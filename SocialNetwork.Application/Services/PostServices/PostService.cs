using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SocialNetwork.Application.DTOs.AccountDTOs;
using SocialNetwork.Application.DTOs.CommonDTOs;
using SocialNetwork.Application.DTOs.PostDTOs;
using SocialNetwork.Application.DTOs.PostMediaDTOs;
using SocialNetwork.Application.Exceptions;
using SocialNetwork.Application.Helpers.FileTypeHelpers;
using SocialNetwork.Application.Services.CloudinaryServices;
using SocialNetwork.Application.Validators;
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
using System.Text.Json;
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
        public async Task<PostDetailResponse> CreatePost(Guid accountId, PostCreateRequest request)
        {
            var account = await _accountRepository.GetAccountById(accountId);
            if (account == null)
                throw new BadRequestException($"Account with ID {accountId} not found.");

            if (account.Status != AccountStatusEnum.Active)
                throw new ForbiddenException("You must reactivate your account to create posts.");

            if (request.Privacy.HasValue &&
                !Enum.IsDefined(typeof(PostPrivacyEnum), request.Privacy.Value))
                throw new BadRequestException("Invalid privacy setting.");

            if (request.FeedAspectRatio.HasValue &&
                !Enum.IsDefined(typeof(AspectRatioEnum), request.FeedAspectRatio.Value))
                throw new BadRequestException("Invalid feed aspect ratio.");



            // ---------- Parse MediaCrops ----------
            List<PostMediaCropInfoRequest> cropInfos = new();
            if (!string.IsNullOrWhiteSpace(request.MediaCrops))
            {
                try
                {
                    cropInfos = JsonSerializer.Deserialize<List<PostMediaCropInfoRequest>>(
                        request.MediaCrops,
                        new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        }
                    ) ?? new();
                }
                catch (JsonException ex)
                {
                    throw new BadRequestException(
                        $"Invalid MediaCrops format. Raw value: {request.MediaCrops}"
                    );
                }
            }

            // ---------- Create Post ----------
            var post = _mapper.Map<Post>(request);
            post.AccountId = accountId;

            // Default FeedAspectRatio = Square (1:1)
            post.FeedAspectRatio = request.FeedAspectRatio.HasValue
                ? (AspectRatioEnum)request.FeedAspectRatio.Value
                : AspectRatioEnum.Square;

            await _postRepository.AddPost(post);

            var result = _mapper.Map<PostDetailResponse>(post);
            result.Owner = _mapper.Map<AccountBasicInfoResponse>(account);

            // ---------- Handle Media ----------
            if (request.MediaFiles != null && request.MediaFiles.Any())
            {
                var uploadTasks = request.MediaFiles.Select(async (media, index) =>
                {
                    var detectedType = await _fileTypeDetector.GetMediaTypeAsync(media);
                    // Optimized: Remove Video support and parallelize uploads
                    if (detectedType == null || detectedType == MediaTypeEnum.Video) return null;

                    string? url = detectedType.Value switch
                    {
                        MediaTypeEnum.Image => await _cloudinaryService.UploadImageAsync(media),
                        _ => null
                    };

                    if (string.IsNullOrEmpty(url)) return null;

                    // Map crop by index
                    var crop = cropInfos.FirstOrDefault(c => c.Index == index);
                    if (crop != null)
                    {
                        MediaCropValidator.Validate(crop, post.FeedAspectRatio);
                    }

                    return new PostMedia
                    {
                        PostId = post.PostId,
                        MediaUrl = url,
                        Type = detectedType.Value,

                        CropX = crop?.CropX,
                        CropY = crop?.CropY,
                        CropWidth = crop?.CropWidth,
                        CropHeight = crop?.CropHeight
                    };
                });

                var uploadedMedias = await Task.WhenAll(uploadTasks);
                var validMedias = uploadedMedias.Where(m => m != null).Cast<PostMedia>().ToList();

                if (validMedias.Any())
                {
                    // Ensure CreatedAt reflects the original selection order, not upload finish time
                    var now = DateTime.UtcNow;
                    for (int i = 0; i < validMedias.Count; i++)
                    {
                        validMedias[i].CreatedAt = now.AddMilliseconds(i * 100);
                    }

                    await _postMediaRepository.AddPostMedias(validMedias);
                    result.Medias = _mapper.Map<List<PostMediaDetailResponse>>(validMedias);
                    result.TotalMedias = result.Medias.Count;
                }
            }

            if (result.Medias == null || !result.Medias.Any())
            {
                throw new BadRequestException("Post must contain at least one valid image. Video files are not supported.");
            }

            result.IsOwner = true;
            await _unitOfWork.CommitAsync();
            return result;
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
