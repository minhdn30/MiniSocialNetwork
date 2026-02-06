using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SocialNetwork.Application.DTOs.AccountDTOs;
using SocialNetwork.Application.DTOs.CommonDTOs;
using SocialNetwork.Application.DTOs.PostDTOs;
using SocialNetwork.Application.Exceptions;
using SocialNetwork.Application.Helpers;
using SocialNetwork.Application.Helpers.FileTypeHelpers;
using SocialNetwork.Application.Services.CloudinaryServices;
using SocialNetwork.Domain.Entities;
using SocialNetwork.Domain.Enums;
using SocialNetwork.Infrastructure.Models;
using SocialNetwork.Infrastructure.Repositories.Accounts;
using SocialNetwork.Infrastructure.Repositories.Comments;
using SocialNetwork.Infrastructure.Repositories.Follows;
using SocialNetwork.Infrastructure.Repositories.PostMedias;
using SocialNetwork.Infrastructure.Repositories.PostReacts;
using SocialNetwork.Infrastructure.Repositories.Posts;
using SocialNetwork.Infrastructure.Repositories.UnitOfWork;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static SocialNetwork.Application.Exceptions.CustomExceptions;


namespace SocialNetwork.Application.Services.PostServices
{
    public class PostService : IPostService
    {
        private readonly IPostRepository _postRepository;
        private readonly IPostMediaRepository _postMediaRepository;
        private readonly IPostReactRepository _postReactRepository;
        private readonly ICommentRepository _commentRepository;
        private readonly IAccountRepository _accountRepository;
        private readonly ICloudinaryService _cloudinaryService;
        private readonly IFileTypeDetector _fileTypeDetector;
        private readonly IMapper _mapper;
        private readonly IUnitOfWork _unitOfWork;
        public PostService(IPostReactRepository postReactRepository,
                           IPostMediaRepository postMediaRepository,
                           IPostRepository postRepository,
                           ICommentRepository commentRepository,
                           IAccountRepository accountRepository,
                           ICloudinaryService cloudinaryService,
                           IFileTypeDetector fileTypeDetector,
                           IMapper mapper,
                           IUnitOfWork unitOfWork)
        {
            _postRepository = postRepository;
            _postMediaRepository = postMediaRepository;
            _postReactRepository = postReactRepository;
            _commentRepository = commentRepository;
            _accountRepository = accountRepository;
            _cloudinaryService = cloudinaryService;
            _fileTypeDetector = fileTypeDetector;
            _mapper = mapper;
            _unitOfWork = unitOfWork;
        }
        public async Task<PostDetailResponse?> GetPostById(Guid postId, Guid? currentId)
        {
            var post = await _postRepository.GetPostById(postId);
            if (post == null)
            {
                throw new NotFoundException($"Post with ID {postId} not found.");
            }
            var result = _mapper.Map<PostDetailResponse>(post);
            result.TotalReacts = await _postReactRepository.GetReactCountByPostId(postId);
            result.TotalComments = await _commentRepository.CountCommentsByPostId(postId);
            result.IsReactedByCurrentUser = await _postReactRepository.IsCurrentUserReactedOnPostAsync(postId, currentId);
            return result;
        }
        //main get by id for frontend
        public async Task<PostDetailModel> GetPostDetailByPostId(Guid postId, Guid currentId)
        {
            var post = await _postRepository.GetPostDetailByPostId(postId, currentId);
            if (post == null)
            {
                throw new NotFoundException($"Post with ID {postId} not found or has been deleted.");
            }
            return post;
        }

        public async Task<PostDetailModel> GetPostDetailByPostCode(string postCode, Guid currentId)
        {
            var post = await _postRepository.GetPostDetailByPostCode(postCode, currentId);
            if (post == null)
            {
                throw new NotFoundException($"Post with code {postCode} not found or has been deleted.");
            }
            return post;
        }

        public async Task<PostDetailResponse> CreatePost(Guid accountId, PostCreateRequest request)
        {
            // 1. Pre-validation and Preparation
            var account = await _accountRepository.GetAccountById(accountId);
            if (account == null)
                throw new BadRequestException($"Account with ID {accountId} not found.");

            if (account.Status != AccountStatusEnum.Active)
                throw new ForbiddenException("You must reactivate your account to create posts.");

            if (request.Privacy.HasValue && !Enum.IsDefined(typeof(PostPrivacyEnum), request.Privacy.Value))
                throw new BadRequestException("Invalid privacy setting.");

            if (request.FeedAspectRatio.HasValue && !Enum.IsDefined(typeof(AspectRatioEnum), request.FeedAspectRatio.Value))
                throw new BadRequestException("Invalid feed aspect ratio.");

            // Generate Unique PostCode
            string postCode = StringHelper.GeneratePostCode(10);
            if (await _postRepository.IsPostCodeExist(postCode)) postCode = StringHelper.GeneratePostCode(12);

            // 2. Upload to Cloudinary (OUTSIDE Transaction) - Sequential for PostgreSQL stability
            var uploadedUrls = new List<string>();
            var validResults = new List<dynamic>();

            if (request.MediaFiles != null && request.MediaFiles.Any())
            {
                // Use sequential loop to avoid DbContext thread safety issues on PostgreSQL
                for (int i = 0; i < request.MediaFiles.Count; i++)
                {
                    var media = request.MediaFiles[i];
                    var detectedType = await _fileTypeDetector.GetMediaTypeAsync(media);
                    if (detectedType != MediaTypeEnum.Image) continue;

                    var url = await _cloudinaryService.UploadImageAsync(media);
                    if (!string.IsNullOrEmpty(url))
                    {
                        validResults.Add(new { Url = url, Type = detectedType.Value, Index = i });
                    }
                }

                if (validResults.Count != request.MediaFiles.Count)
                    throw new BadRequestException("Some images failed to upload or are invalid. Videos are not supported.");

                // Map to domain entities
                var now = DateTime.UtcNow;
                var mediaEntities = validResults.Select(r => {
                    var current = r!;
                    uploadedUrls.Add(current.Url!);
                    return new PostMedia
                    {
                        MediaUrl = current.Url!,
                        Type = current.Type,
                        CreatedAt = now.AddMilliseconds(current.Index * 100)
                    };
                }).ToList();

                // 3. DB Transaction (Atomic Post + Media)
                using var transaction = await _unitOfWork.BeginTransactionAsync();
                try
                {
                    var post = _mapper.Map<Post>(request);
                    post.AccountId = accountId;
                    post.PostCode = postCode;
                    post.FeedAspectRatio = (AspectRatioEnum)(request.FeedAspectRatio ?? (int)AspectRatioEnum.Square);
                    post.Medias = mediaEntities;

                    await _postRepository.AddPost(post);
                    
                    // CRITICAL: Ensure data is written to DB before committing the transaction
                    await _unitOfWork.CommitAsync();
                    await transaction.CommitAsync();

                    var result = _mapper.Map<PostDetailResponse>(post);
                    result.Owner = _mapper.Map<AccountBasicInfoResponse>(account);
                    result.IsOwner = true;
                    return result;
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync();
                    
                    // Cleanup: Delete orphaned images from Cloudinary if DB fail
                    var cleanupTasks = uploadedUrls.Select(url => {
                        var pid = _cloudinaryService.GetPublicIdFromUrl(url);
                        return !string.IsNullOrEmpty(pid) ? _cloudinaryService.DeleteMediaAsync(pid, MediaTypeEnum.Image) : Task.CompletedTask;
                    });
                    await Task.WhenAll(cleanupTasks);
                    throw;
                }
            }

            throw new BadRequestException("Post must contain at least one valid image.");
        }


        public async Task<PostDetailResponse> UpdatePost(Guid postId, Guid currentId, PostUpdateRequest request)
        {
            var post = await _postRepository.GetPostById(postId);
            if (post == null)
            {
                throw new NotFoundException($"Post with ID {postId} not found.");
            }
            if (request.Privacy.HasValue && !Enum.IsDefined(typeof(PostPrivacyEnum), request.Privacy.Value))
            {
                throw new BadRequestException("Invalid privacy setting.");
            }
            if(post.AccountId != currentId)
            {
                throw new ForbiddenException("You are not authorized to update this post.");
            }

            if (post.Account.Status != AccountStatusEnum.Active)
                throw new ForbiddenException("You must reactivate your account to update posts.");
            _mapper.Map(request, post);
            post.UpdatedAt = DateTime.UtcNow;
            //remove medias if any
            if (request.RemoveMediaIds != null && request.RemoveMediaIds.Any())
            {
                var removedMedias = new List<PostMedia>();
                foreach (var mediaId in request.RemoveMediaIds)
                {
                    var media = await _postMediaRepository.GetPostMediaById(mediaId);
                    if (media != null && media.PostId == postId)
                    {
                        if (!string.IsNullOrEmpty(media.MediaUrl))
                        {
                            var publicId = _cloudinaryService.GetPublicIdFromUrl(media.MediaUrl);
                            if (!string.IsNullOrEmpty(publicId))
                            {
                                await _cloudinaryService.DeleteMediaAsync(publicId, media.Type);                               
                            }
                        }
                        removedMedias.Add(media);
                    }
                }
                if (removedMedias.Any())
                {
                    await _postMediaRepository.DeletePostMedias(removedMedias);
                }
            }
            //add new medias if any
            if (request.NewMediaFiles != null && request.NewMediaFiles.Any())
            {
                var newPostMedias = new List<PostMedia>();
                foreach (var media in request.NewMediaFiles)
                {
                    var detectedType = await _fileTypeDetector.GetMediaTypeAsync(media);
                    if (detectedType == null) continue;  // Skip unsupported media types
                    string? url = null;
                    switch (detectedType.Value)
                    {
                        case MediaTypeEnum.Image:
                            url = await _cloudinaryService.UploadImageAsync(media);
                            break;
                        case MediaTypeEnum.Video:
                            url = await _cloudinaryService.UploadVideoAsync(media);
                            break;
                        //Audio and Document upload later
                        default:
                            continue;
                    };
                    if (!string.IsNullOrEmpty(url))
                    {
                        var newMedia = new PostMedia
                        {
                            PostId = post.PostId,
                            MediaUrl = url,
                            Type = detectedType.Value
                        };
                        newPostMedias.Add(newMedia);
                        post.Medias.Add(newMedia);
                    }
                }
                if (newPostMedias.Any())
                {
                    await _postMediaRepository.AddPostMedias(newPostMedias);
                }
            }

            await _postRepository.UpdatePost(post);
            var account = await _accountRepository.GetAccountById(post.AccountId);
            var result = _mapper.Map<PostDetailResponse>(post);
            result.TotalReacts = await _postReactRepository.GetReactCountByPostId(postId);
            result.TotalComments = await _commentRepository.CountCommentsByPostId(postId);
            result.IsReactedByCurrentUser = await _postReactRepository.IsCurrentUserReactedOnPostAsync(postId, currentId);
            return result;

        }
        public async Task<PostUpdateContentResponse> UpdatePostContent(Guid postId, Guid currentId, PostUpdateContentRequest request)
        {
            var post = await _postRepository.GetPostForUpdateContent(postId);
            if (post == null)
            {
                throw new NotFoundException($"Post with ID {postId} not found.");
            }
            if (request.Privacy.HasValue && !Enum.IsDefined(typeof(PostPrivacyEnum), request.Privacy.Value))
            {
                throw new BadRequestException("Invalid privacy setting.");
            }
            if (post.AccountId != currentId)
            {
                throw new ForbiddenException("You are not authorized to update this post.");
            }
            if (request.Content != null && post.Content != request.Content)
            {
                post.Content = request.Content;
                post.UpdatedAt = DateTime.UtcNow;
            }
            
            if (request.Privacy.HasValue)
            {
                post.Privacy = (PostPrivacyEnum)request.Privacy.Value;
            }

            // Check if Post becomes empty
            if (string.IsNullOrWhiteSpace(post.Content) && (post.Medias == null || !post.Medias.Any()))
            {
                throw new BadRequestException("Post must have content or media files.");
            }

            await _postRepository.UpdatePost(post);
            
            return new PostUpdateContentResponse
            {
                PostId = post.PostId,
                Content = post.Content,
                Privacy = post.Privacy,
                UpdatedAt = post.UpdatedAt
            };
        }
        public async Task<Guid?> SoftDeletePost(Guid postId, Guid currentId, bool isAdmin)
        {
            var post = await _postRepository.GetPostBasicInfoById(postId);
            if (post == null)
            {
                throw new NotFoundException($"Post with ID {postId} not found.");
            }
            if(post.AccountId != currentId && !isAdmin)
            {
                throw new ForbiddenException("You are not authorized to delete this post.");
            }

            await _postRepository.SoftDeletePostAsync(postId);
            return post.AccountId;
        }
        public async Task<PagedResponse<PostPersonalListModel>> GetPostsByAccountId(Guid accountId, Guid? currentId, int page, int pageSize)
        {
            if (!await _accountRepository.IsAccountIdExist(accountId))
                throw new NotFoundException($"Account with ID {accountId} does not exist.");

            var (items, total) = await _postRepository.GetPostsByAccountId(accountId, currentId, page, pageSize);
            return new PagedResponse<PostPersonalListModel>
            {
                Items = items,
                TotalItems = total,
                Page = page,
                PageSize = pageSize
            };
        }
        public async Task<List<PostFeedModel>> GetFeedByScoreAsync(Guid currentId, DateTime? cursorCreatedAt, Guid? cursorPostId, int limit)
        {
            if(limit <= 0) limit = 10;
            if(limit > 50) limit = 50;
            var feed = await _postRepository.GetFeedByScoreAsync(currentId, cursorCreatedAt, cursorPostId, limit);
            return feed;
        }
    }
}
