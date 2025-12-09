using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using SocialNetwork.Application.DTOs.AccountDTOs;
using SocialNetwork.Application.DTOs.PostDTOs;
using SocialNetwork.Application.DTOs.PostMediaDTOs;
using SocialNetwork.Application.Helpers.FileTypeHelpers;
using SocialNetwork.Application.Services.CloudinaryServices;
using SocialNetwork.Domain.Entities;
using SocialNetwork.Domain.Enums;
using SocialNetwork.Infrastructure.Repositories.Accounts;
using SocialNetwork.Infrastructure.Repositories.Follows;
using SocialNetwork.Infrastructure.Repositories.PostMedias;
using SocialNetwork.Infrastructure.Repositories.PostReacts;
using SocialNetwork.Infrastructure.Repositories.Posts;
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
        private readonly IAccountRepository _accountRepository;
        private readonly ICloudinaryService _cloudinaryService;
        private readonly IFileTypeDetector _fileTypeDetector;
        private readonly IMapper _mapper;
        public PostService(IPostReactRepository postReactRepository,
                           IPostMediaRepository postMediaRepository,
                           IPostRepository postRepository,
                           IAccountRepository accountRepository,
                           ICloudinaryService cloudinaryService,
                           IFileTypeDetector fileTypeDetector,
                           IMapper mapper)
        {
            _postRepository = postRepository;
            _postMediaRepository = postMediaRepository;
            _postReactRepository = postReactRepository;
            _accountRepository = accountRepository;
            _cloudinaryService = cloudinaryService;
            _fileTypeDetector = fileTypeDetector;
            _mapper = mapper;
        }
        public async Task<PostDetailResponse?> GetPostById(Guid postId)
        {
            var post = await _postRepository.GetPostById(postId);
            if (post == null)
            {
                throw new NotFoundException($"Post with ID {postId} not found.");
            }
            var result = _mapper.Map<PostDetailResponse>(post);
            result.Owner = _mapper.Map<AccountPostDetailResponse>(post.Account);
            result.Medias = _mapper.Map<List<PostMediaDetailResponse>>(post.Medias);
            return result;
        }
        public async Task<PostDetailResponse> CreatePost([FromBody] PostCreateRequest request)
        {
            var account = await _accountRepository.GetAccountById(request.AccountId);
            if (account == null)
            {
                throw new BadRequestException($"Account with ID {request.AccountId} not found.");
            }
            if(request.Privacy.HasValue && !Enum.IsDefined(typeof(PostPrivacyEnum), request.Privacy.Value))
            {
                throw new BadRequestException("Invalid privacy setting.");
            }
            var post = _mapper.Map<Post>(request);
            await _postRepository.AddPost(post);
            var result = _mapper.Map<PostDetailResponse>(post);
            result.Owner = _mapper.Map<AccountPostDetailResponse>(account);
            if (request.MediaFiles != null && request.MediaFiles.Any())
            {
                var postMedias = new List<PostMedia>();
                foreach (var media in request.MediaFiles)
                {
                    var detectedType = await _fileTypeDetector.GetMediaTypeAsync(media);
                    if (detectedType == null) continue; // Skip unsupported media types
                    string? url = null;
                    switch(detectedType.Value)
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
                    }
                    ;

                    if (!string.IsNullOrEmpty(url))
                    {
                        postMedias.Add(new PostMedia
                        {
                            PostId = post.PostId,
                            MediaUrl = url,
                            Type = detectedType.Value
                        });
                    }
                }
                if(postMedias.Any())
                {
                    await _postMediaRepository.AddPostMedias(postMedias);
                    result.Medias = _mapper.Map<List<PostMediaDetailResponse>>(postMedias);
                }
            }

            return result;
        }
  
        public async Task<PostDetailResponse> UpdatePost(Guid postId, [FromBody] PostUpdateRequest request)
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
            result.Owner = _mapper.Map<AccountPostDetailResponse>(account);
            result.Medias = _mapper.Map<List<PostMediaDetailResponse>>(post.Medias);
            return result;

        }
        public async Task SoftDeletePost(Guid postId)
        {
            var post = await _postRepository.GetPostById(postId);
            if (post == null)
            {
                throw new NotFoundException($"Post with ID {postId} not found.");
            }
            await _postRepository.SoftDeletePostAsync(postId);
        }
    }
}
