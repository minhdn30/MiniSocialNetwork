using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SocialNetwork.Application.DTOs.AccountDTOs;
using SocialNetwork.Application.DTOs.CommonDTOs;
using SocialNetwork.Application.DTOs.PostDTOs;
using SocialNetwork.Application.DTOs.PostMediaDTOs;
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
            result.IsReactedByCurrentUser = await _postReactRepository.IsCurrentUserReactedOnPostAsync(postId, currentId);
            return result;
        }
        public async Task<PostDetailResponse> CreatePost(Guid accountId, PostCreateRequest request)
        {
            var account = await _accountRepository.GetAccountById(accountId);
            if (account == null)
                throw new BadRequestException($"Account with ID {accountId} not found.");

            if (request.Privacy.HasValue &&
                !Enum.IsDefined(typeof(PostPrivacyEnum), request.Privacy.Value))
                throw new BadRequestException("Invalid privacy setting.");

            if (request.FeedAspectRatio.HasValue &&
                !Enum.IsDefined(typeof(AspectRatioEnum), request.FeedAspectRatio.Value))
                throw new BadRequestException("Invalid feed aspect ratio.");

            if (request.Content == null &&
                (request.MediaFiles == null || !request.MediaFiles.Any()))
                throw new BadRequestException("Post must have content or media files.");

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
                var postMedias = new List<PostMedia>();

                for (int i = 0; i < request.MediaFiles.Count; i++)
                {
                    var media = request.MediaFiles[i];

                    var detectedType = await _fileTypeDetector.GetMediaTypeAsync(media);
                    if (detectedType == null) continue;

                    string? url = detectedType.Value switch
                    {
                        MediaTypeEnum.Image => await _cloudinaryService.UploadImageAsync(media),
                        MediaTypeEnum.Video => await _cloudinaryService.UploadVideoAsync(media),
                        _ => null
                    };

                    if (string.IsNullOrEmpty(url)) continue;

                    // Map crop by index
                    var crop = cropInfos.FirstOrDefault(c => c.Index == i);
                    if (crop != null)
                    {
                        MediaCropValidator.Validate(crop, post.FeedAspectRatio);
                    }
                    postMedias.Add(new PostMedia
                    {
                        PostId = post.PostId,
                        MediaUrl = url,
                        Type = detectedType.Value,

                        CropX = crop?.CropX,
                        CropY = crop?.CropY,
                        CropWidth = crop?.CropWidth,
                        CropHeight = crop?.CropHeight
                    });
                }

                if (postMedias.Any())
                {
                    await _postMediaRepository.AddPostMedias(postMedias);
                    result.Medias = _mapper.Map<List<PostMediaDetailResponse>>(postMedias);
                    result.TotalMedias = result.Medias.Count;
                }
            }
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
            //can use this for performance improvement
            //result.TotalReacts = await _postReactRepository.GetReactCountByPostId(postId);
            //result.TotalComments = await _commentRepository.CountCommentsByPostId(postId);
            result.IsReactedByCurrentUser = await _postReactRepository.IsCurrentUserReactedOnPostAsync(postId, currentId);
            return result;

        }
        public async Task<Guid?> SoftDeletePost(Guid postId, Guid currentId, bool isAdmin)
        {
            var post = await _postRepository.GetPostById(postId);
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
