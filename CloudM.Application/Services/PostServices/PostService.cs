using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CloudM.Application.DTOs.AccountDTOs;
using CloudM.Application.DTOs.CommonDTOs;
using CloudM.Application.DTOs.PostDTOs;
using CloudM.Application.DTOs.PostMediaDTOs;
using CloudM.Domain.Exceptions;
using CloudM.Application.Helpers.FileTypeHelpers;
using CloudM.Application.Helpers.StoryHelpers;
using CloudM.Application.Helpers.SwaggerHelpers;
using CloudM.Infrastructure.Services.Cloudinary;
using CloudM.Application.Services.RealtimeServices;
using CloudM.Application.Services.StoryViewServices;
using CloudM.Domain.Entities;
using CloudM.Domain.Enums;
using CloudM.Infrastructure.Models;
using CloudM.Infrastructure.Repositories.Accounts;
using CloudM.Infrastructure.Repositories.Comments;
using CloudM.Infrastructure.Repositories.Follows;
using CloudM.Infrastructure.Repositories.PostMedias;
using CloudM.Infrastructure.Repositories.PostReacts;
using CloudM.Infrastructure.Repositories.PostSaves;
using CloudM.Infrastructure.Repositories.Posts;
using CloudM.Infrastructure.Repositories.UnitOfWork;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static CloudM.Domain.Exceptions.CustomExceptions;


namespace CloudM.Application.Services.PostServices
{
    public class PostService : IPostService
    {
        private const int MaxPostTagCount = 20;
        private const int MaxTagErrorDisplayCount = 5;

        private readonly IPostRepository _postRepository;
        private readonly IPostMediaRepository _postMediaRepository;
        private readonly IPostReactRepository _postReactRepository;
        private readonly IPostSaveRepository _postSaveRepository;
        private readonly ICommentRepository _commentRepository;
        private readonly IAccountRepository _accountRepository;
        private readonly IFollowRepository _followRepository;
        private readonly ICloudinaryService _cloudinaryService;
        private readonly IFileTypeDetector _fileTypeDetector;
        private readonly IMapper _mapper;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IRealtimeService _realtimeService;
        private readonly IStoryRingStateHelper _storyRingStateHelper;
        public PostService(IPostReactRepository postReactRepository,
                           IPostSaveRepository postSaveRepository,
                           IPostMediaRepository postMediaRepository,
                           IPostRepository postRepository,
                           ICommentRepository commentRepository,
                           IAccountRepository accountRepository,
                           IFollowRepository followRepository,
                           ICloudinaryService cloudinaryService,
                           IFileTypeDetector fileTypeDetector,
                           IMapper mapper,
                           IUnitOfWork unitOfWork,
                           IRealtimeService realtimeService,
                           IStoryViewService storyViewService,
                           IStoryRingStateHelper? storyRingStateHelper = null)
        {
            _postRepository = postRepository;
            _postMediaRepository = postMediaRepository;
            _postReactRepository = postReactRepository;
            _postSaveRepository = postSaveRepository;
            _commentRepository = commentRepository;
            _accountRepository = accountRepository;
            _followRepository = followRepository;
            _cloudinaryService = cloudinaryService;
            _fileTypeDetector = fileTypeDetector;
            _mapper = mapper;
            _unitOfWork = unitOfWork;
            _realtimeService = realtimeService;
            _storyRingStateHelper = storyRingStateHelper ?? new StoryRingStateHelper(storyViewService);
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
            result.IsSavedByCurrentUser = currentId.HasValue && await _postSaveRepository.IsPostSavedByCurrentAsync(currentId.Value, postId);
            return result;
        }

        public async Task<List<PostTaggedAccountResponse>> GetTaggedAccountsByPostId(Guid postId, Guid currentId)
        {
            var taggedAccounts = await _postRepository.GetTaggedAccountsByPostIdAsync(postId, currentId);
            if (taggedAccounts == null)
            {
                throw new NotFoundException($"Post with ID {postId} not found or has been deleted.");
            }

            return taggedAccounts
                .Select(x => new PostTaggedAccountResponse
                {
                    AccountId = x.AccountId,
                    Username = x.Username,
                    FullName = x.FullName,
                    AvatarUrl = x.AvatarUrl,
                    IsFollowing = x.IsFollowing,
                    IsFollower = x.IsFollower
                })
                .ToList();
        }

        public async Task<PostDetailModel> GetPostDetailByPostId(Guid postId, Guid currentId)
        {
            var post = await _postRepository.GetPostDetailByPostId(postId, currentId);
            if (post == null)
            {
                throw new NotFoundException($"Post with ID {postId} not found or has been deleted.");
            }

            await ApplyStoryRingStateForOwnerAsync(currentId, post.Owner);
            return post;
        }

        public async Task<PostDetailModel> GetPostDetailByPostCode(string postCode, Guid currentId)
        {
            var post = await _postRepository.GetPostDetailByPostCode(postCode, currentId);
            if (post == null)
            {
                throw new NotFoundException($"Post with code {postCode} not found or has been deleted.");
            }

            await ApplyStoryRingStateForOwnerAsync(currentId, post.Owner);
            return post;
        }

        public async Task<PostDetailResponse> CreatePost(Guid accountId, PostCreateRequest request)
        {
            // Pre-validation and Preparation
            var account = await _accountRepository.GetAccountById(accountId);
            if (account == null)
                throw new BadRequestException($"Account with ID {accountId} not found.");

            if (account.Status != AccountStatusEnum.Active)
                throw new ForbiddenException("You must reactivate your account to create posts.");

            var normalizedTagIds = NormalizePostTagIds(request.TaggedAccountIds, accountId);
            if (normalizedTagIds.Count > MaxPostTagCount)
            {
                throw new BadRequestException($"You can tag up to {MaxPostTagCount} accounts in one post.");
            }

            var effectiveCreatePrivacy = request.Privacy.HasValue
                ? (PostPrivacyEnum)request.Privacy.Value
                : PostPrivacyEnum.Public;
            var validatedTaggedAccounts = await ValidateTaggedAccountsAsync(normalizedTagIds);
            await EnsureTaggedAccountsCompatibleWithPrivacyAsync(
                accountId,
                effectiveCreatePrivacy,
                normalizedTagIds,
                validatedTaggedAccounts);

            // Generate Unique PostCode
            string postCode = StringHelper.GeneratePostCode(10);
            if (await _postRepository.IsPostCodeExist(postCode)) postCode = StringHelper.GeneratePostCode(12);

            // Upload to Cloudinary (OUTSIDE Transaction) - Parallel for better performance
            // Note: Parallel upload is safe here because Cloudinary calls don't use DbContext
            var uploadedUrls = new List<string>();
            var validResults = new List<(string Url, MediaTypeEnum Type, int Index)>();

            if (request.MediaFiles != null && request.MediaFiles.Any())
            {
                // First, validate all file types synchronously (fast operation)
                var mediaWithTypes = new List<(Microsoft.AspNetCore.Http.IFormFile File, MediaTypeEnum Type, int Index)>();
                for (int i = 0; i < request.MediaFiles.Count; i++)
                {
                    var media = request.MediaFiles[i];
                    var detectedType = await _fileTypeDetector.GetMediaTypeAsync(media);
                    if (detectedType != MediaTypeEnum.Image)
                    {
                        throw new BadRequestException($"File at position {i + 1} is not a valid image. Videos are not supported.");
                    }
                    mediaWithTypes.Add((media, detectedType.Value, i));
                }

                // Parallel upload to Cloudinary (IO-bound, safe to parallelize)
                var uploadTasks = mediaWithTypes.Select(async item =>
                {
                    var url = await _cloudinaryService.UploadImageAsync(item.File);
                    return (Url: url, Type: item.Type, Index: item.Index);
                });

                var results = await Task.WhenAll(uploadTasks);

                // Validate all uploads succeeded
                foreach (var result in results)
                {
                    if (string.IsNullOrEmpty(result.Url))
                    {
                        throw new BadRequestException($"Failed to upload image at position {result.Index + 1}.");
                    }
                    validResults.Add((result.Url!, result.Type, result.Index));
                }

                if (validResults.Count != request.MediaFiles.Count)
                    throw new BadRequestException("Some images failed to upload or are invalid. Videos are not supported.");

                // Map to domain entities (ordered by original index)
                var now = DateTime.UtcNow;
                var mediaEntities = validResults
                    .OrderBy(r => r.Index)
                    .Select(r => {
                        uploadedUrls.Add(r.Url);
                        return new PostMedia
                        {
                            MediaUrl = r.Url,
                            Type = r.Type,
                            CreatedAt = now.AddMilliseconds(r.Index * 100)
                        };
                    }).ToList();


                // DB Transaction (Atomic Post + Media)
                return await _unitOfWork.ExecuteInTransactionAsync(
                    async () =>
                    {
                        var post = _mapper.Map<Post>(request);
                        post.AccountId = accountId;
                        post.PostCode = postCode;
                        post.FeedAspectRatio = (AspectRatioEnum)(request.FeedAspectRatio ?? (int)AspectRatioEnum.Square);
                        post.Medias = mediaEntities;
                        if (normalizedTagIds.Count > 0)
                        {
                            var nowUtc = DateTime.UtcNow;
                            post.Tags = normalizedTagIds
                                .Select((taggedAccountId, index) => new PostTag
                                {
                                    PostId = post.PostId,
                                    TaggedAccountId = taggedAccountId,
                                    CreatedAt = nowUtc.AddMilliseconds(index)
                                })
                                .ToList();
                        }

                        await _postRepository.AddPost(post);

                        // Commit explicitly before sending notification to ensure data availability
                        await _unitOfWork.CommitAsync();

                        var result = _mapper.Map<PostDetailResponse>(post);
                        result.Owner = _mapper.Map<AccountBasicInfoResponse>(account);
                        result.IsOwner = true;
                        result.TotalReacts = 0;
                        result.TotalComments = 0;
                        result.IsReactedByCurrentUser = false;
                        result.IsSavedByCurrentUser = false;
                        result.TaggedAccounts = MapTaggedAccounts(validatedTaggedAccounts);
                        
                        // Ensure medias are mapped (if not done by AutoMapper)
                        if (post.Medias != null && (result.Medias == null || !result.Medias.Any()))
                        {
                            result.Medias = post.Medias.Select(m => new PostMediaDetailResponse
                            {
                                MediaId = m.MediaId,
                                MediaUrl = m.MediaUrl,
                                Type = m.Type,
                                CreatedAt = m.CreatedAt
                            }).ToList();
                        }
                        result.TotalMedias = result.Medias?.Count ?? 0;

                        // Send realtime notification
                        await _realtimeService.NotifyPostCreatedAsync(accountId, result);

                        return result;
                    },
                    // Cleanup callback: delete orphaned images from Cloudinary if DB fail
                    async () =>
                    {
                        var cleanupTasks = uploadedUrls.Select(url =>
                        {
                            var pid = _cloudinaryService.GetPublicIdFromUrl(url);
                            return !string.IsNullOrEmpty(pid) ? _cloudinaryService.DeleteMediaAsync(pid, MediaTypeEnum.Image) : Task.CompletedTask;
                        });
                        await Task.WhenAll(cleanupTasks);
                    }
                );
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

            if(post.AccountId != currentId)
            {
                throw new ForbiddenException("You are not authorized to update this post.");
            }

            if (post.Account.Status != AccountStatusEnum.Active)
                throw new ForbiddenException("You must reactivate your account to update posts.");

            if ((request.RemoveMediaIds?.Any() ?? false) || (request.NewMediaFiles?.Any() ?? false))
                throw new BadRequestException("Updating media is not supported. You can only update content and privacy.");

            if (request.Content != null && post.Content != request.Content)
            {
                post.Content = request.Content;
            }

            if (request.Privacy.HasValue)
            {
                post.Privacy = (PostPrivacyEnum)request.Privacy.Value;
            }

            if (post.Medias == null || !post.Medias.Any())
            {
                throw new BadRequestException("Post must contain at least one media file.");
            }

            post.UpdatedAt = DateTime.UtcNow;

            await _postRepository.UpdatePost(post);
            await _unitOfWork.CommitAsync();

            var account = await _accountRepository.GetAccountById(post.AccountId);
            var result = _mapper.Map<PostDetailResponse>(post);
            result.TotalReacts = await _postReactRepository.GetReactCountByPostId(postId);
            result.TotalComments = await _commentRepository.CountCommentsByPostId(postId);
            result.IsReactedByCurrentUser = await _postReactRepository.IsCurrentUserReactedOnPostAsync(postId, currentId);
            result.IsSavedByCurrentUser = await _postSaveRepository.IsPostSavedByCurrentAsync(currentId, postId);

            // Send realtime notification
            await _realtimeService.NotifyPostUpdatedAsync(postId, currentId, result);

            return result;

        }
        public async Task<PostUpdateContentResponse> UpdatePostContent(Guid postId, Guid currentId, PostUpdateContentRequest request)
        {
            var post = await _postRepository.GetPostForUpdateContent(postId);
            if (post == null)
            {
                throw new NotFoundException($"Post with ID {postId} not found.");
            }

            if (post.AccountId != currentId)
            {
                throw new ForbiddenException("You are not authorized to update this post.");
            }

            var shouldPersistPostFields = false;
            var hasTagChanged = false;
            if (request.Content != null)
            {
                shouldPersistPostFields = true;
                if (post.Content != request.Content)
                {
                    post.Content = request.Content;
                    // keep original behavior: UpdatedAt changes only when content changes
                    post.UpdatedAt = DateTime.UtcNow;
                }
            }
            
            if (request.Privacy.HasValue)
            {
                var nextPrivacy = (PostPrivacyEnum)request.Privacy.Value;
                post.Privacy = nextPrivacy;
                shouldPersistPostFields = true;
            }

            var normalizedAddTagIds = NormalizePostTagIds(request.AddNewTagIds, currentId);
            var normalizedRemoveTagIds = NormalizePostTagIds(request.RemoveTagIds, currentId);
            var hasTagMutationInput = normalizedAddTagIds.Count > 0 || normalizedRemoveTagIds.Count > 0;
            var effectivePrivacy = request.Privacy.HasValue
                ? (PostPrivacyEnum)request.Privacy.Value
                : post.Privacy;

            if (normalizedAddTagIds.Count > 0 && normalizedRemoveTagIds.Count > 0)
            {
                var duplicatedIds = normalizedAddTagIds
                    .Intersect(normalizedRemoveTagIds)
                    .ToList();
                if (duplicatedIds.Count > 0)
                {
                    throw new BadRequestException("A tagged account cannot be included in both addNewTagIds and removeTagIds.");
                }
            }

            if (hasTagMutationInput)
            {
                var existingTagIds = (await _postRepository.GetTaggedAccountIdsByPostIdAsync(postId))
                    ?? new List<Guid>();
                var existingTagIdSet = existingTagIds.ToHashSet();

                var removeTagIds = normalizedRemoveTagIds
                    .Where(existingTagIdSet.Contains)
                    .Distinct()
                    .ToList();

                var addTagIds = normalizedAddTagIds
                    .Where(x => !existingTagIdSet.Contains(x))
                    .Distinct()
                    .ToList();

                var finalTagIds = existingTagIds
                    .Where(x => !removeTagIds.Contains(x))
                    .Concat(addTagIds)
                    .Distinct()
                    .ToList();

                if (finalTagIds.Count > MaxPostTagCount)
                {
                    throw new BadRequestException($"You can tag up to {MaxPostTagCount} accounts in one post.");
                }

                if (addTagIds.Count > 0)
                {
                    var validatedAddedAccounts = await ValidateTaggedAccountsAsync(addTagIds);
                    await EnsureTaggedAccountsCompatibleWithPrivacyAsync(
                        post.AccountId,
                        effectivePrivacy,
                        addTagIds,
                        validatedAddedAccounts);
                }

                if (removeTagIds.Count > 0)
                {
                    await _postRepository.RemovePostTagsAsync(postId, removeTagIds);
                    hasTagChanged = true;
                }

                if (addTagIds.Count > 0)
                {
                    var nowUtc = DateTime.UtcNow;
                    var newTags = addTagIds
                        .Select((taggedAccountId, index) => new PostTag
                        {
                            PostId = postId,
                            TaggedAccountId = taggedAccountId,
                            CreatedAt = nowUtc.AddMilliseconds(index)
                        })
                        .ToList();

                    await _postRepository.AddPostTagsAsync(newTags);
                    hasTagChanged = true;
                }
            }

            if (post.Medias == null || !post.Medias.Any())
            {
                throw new BadRequestException("Post must contain at least one media file.");
            }

            if (shouldPersistPostFields)
            {
                await _postRepository.UpdatePost(post);
            }

            if (shouldPersistPostFields || hasTagChanged)
            {
                await _unitOfWork.CommitAsync();
            }
            
            var result = new PostUpdateContentResponse
            {
                PostId = post.PostId,
                Content = post.Content,
                Privacy = post.Privacy,
                UpdatedAt = post.UpdatedAt
            };

            if (shouldPersistPostFields || hasTagChanged)
            {
                // Send realtime notification
                await _realtimeService.NotifyPostContentUpdatedAsync(postId, currentId, result);
            }

            return result;
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
            await _unitOfWork.CommitAsync();

            // Send realtime notification
            await _realtimeService.NotifyPostDeletedAsync(postId, post.AccountId);

            return post.AccountId;
        }
        public async Task<(List<PostPersonalListModel> Items, bool HasMore)> GetPostsByAccountIdByCursorAsync(
            Guid accountId,
            Guid? currentId,
            DateTime? cursorCreatedAt,
            Guid? cursorPostId,
            int limit)
        {
            if (limit <= 0) limit = 10;
            if (limit > 50) limit = 50;

            if (!await _accountRepository.IsAccountIdExist(accountId))
                throw new NotFoundException($"Account with ID {accountId} does not exist.");

            var candidates = await _postRepository.GetPostsByAccountIdByCursor(
                accountId,
                currentId,
                cursorCreatedAt,
                cursorPostId,
                limit + 1);

            var hasMore = candidates.Count > limit;
            var items = hasMore ? candidates.Take(limit).ToList() : candidates;

            return (items, hasMore);
        }
        public async Task<(List<PostPersonalListModel> Items, bool HasMore)> GetTaggedPostsByAccountIdByCursorAsync(
            Guid accountId,
            Guid currentId,
            DateTime? cursorCreatedAt,
            Guid? cursorPostId,
            int limit)
        {
            if (limit <= 0) limit = 10;
            if (limit > 50) limit = 50;

            if (!await _accountRepository.IsAccountIdExist(accountId))
                throw new NotFoundException($"Account with ID {accountId} does not exist.");

            var candidates = await _postRepository.GetTaggedPostsByAccountIdByCursor(
                accountId,
                currentId,
                cursorCreatedAt,
                cursorPostId,
                limit + 1);

            var hasMore = candidates.Count > limit;
            var items = hasMore ? candidates.Take(limit).ToList() : candidates;

            return (items, hasMore);
        }
        public async Task<List<PostFeedModel>> GetFeedByScoreAsync(Guid currentId, DateTime? cursorCreatedAt, Guid? cursorPostId, int limit)
        {
            if (limit <= 0) limit = 10;
            if (limit > 50) limit = 50;

            var feed = await _postRepository.GetFeedByScoreAsync(
                currentId,
                cursorCreatedAt,
                cursorPostId,
                limit);

            if (feed.Count == 0)
            {
                return feed;
            }

            var authorIds = feed
                .Select(x => x.Author.AccountId)
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList();

            var storyRingStateMap = await _storyRingStateHelper.ResolveManyAsync(currentId, authorIds);

            foreach (var post in feed)
            {
                post.Author.StoryRingState = storyRingStateMap.TryGetValue(post.Author.AccountId, out var ringState)
                    ? ringState
                    : StoryRingStateEnum.None;
            }

            return feed;
        }

        private async Task ApplyStoryRingStateForOwnerAsync(Guid currentId, AccountBasicInfoModel? owner)
        {
            if (owner == null || owner.AccountId == Guid.Empty)
            {
                return;
            }

            owner.StoryRingState = await _storyRingStateHelper.ResolveAsync(currentId, owner.AccountId);
        }

        private static List<Guid> NormalizePostTagIds(IEnumerable<Guid>? tagIds, Guid currentId)
        {
            return (tagIds ?? Enumerable.Empty<Guid>())
                .Where(x => x != Guid.Empty && x != currentId)
                .Distinct()
                .ToList();
        }

        private async Task<List<Account>> ValidateTaggedAccountsAsync(List<Guid> taggedAccountIds)
        {
            if (taggedAccountIds.Count == 0)
            {
                return new List<Account>();
            }

            var accounts = await _accountRepository.GetAccountsByIds(taggedAccountIds);
            var activeAccountMap = accounts
                .Where(x => x.Status == AccountStatusEnum.Active)
                .ToDictionary(x => x.AccountId, x => x);

            var notFoundOrInactiveIds = taggedAccountIds
                .Where(x => !activeAccountMap.ContainsKey(x))
                .ToList();
            if (notFoundOrInactiveIds.Count > 0)
            {
                throw new BadRequestException("Some selected accounts are unavailable for tagging.");
            }

            var disallowedAccounts = taggedAccountIds
                .Select(x => activeAccountMap[x])
                .Where(x => x.Settings?.TagPermission == TagPermissionEnum.NoOne)
                .ToList();

            if (disallowedAccounts.Count > 0)
            {
                throw new BadRequestException(BuildTagPermissionDeniedMessage(disallowedAccounts));
            }

            return taggedAccountIds
                .Select(x => activeAccountMap[x])
                .ToList();
        }

        private async Task EnsureTaggedAccountsCompatibleWithPrivacyAsync(
            Guid ownerId,
            PostPrivacyEnum privacy,
            List<Guid> taggedAccountIds,
            IEnumerable<Account>? taggedAccounts = null)
        {
            if (taggedAccountIds.Count == 0)
            {
                return;
            }

            if (privacy == PostPrivacyEnum.Private)
            {
                throw new BadRequestException("You cannot tag people on a private post.");
            }

            if (privacy != PostPrivacyEnum.FollowOnly)
            {
                return;
            }

            var followerIdSet = await _followRepository.GetFollowerIdsInTargetsAsync(ownerId, taggedAccountIds);
            var disallowedTagIds = taggedAccountIds
                .Where(x => !followerIdSet.Contains(x))
                .ToList();

            if (disallowedTagIds.Count > 0)
            {
                var accountMap = (taggedAccounts ?? Enumerable.Empty<Account>())
                    .GroupBy(x => x.AccountId)
                    .ToDictionary(x => x.Key, x => x.First());

                var missingIds = disallowedTagIds
                    .Where(x => !accountMap.ContainsKey(x))
                    .ToList();

                if (missingIds.Count > 0)
                {
                    var accounts = await _accountRepository.GetAccountsByIds(missingIds);
                    foreach (var account in accounts)
                    {
                        accountMap[account.AccountId] = account;
                    }
                }

                throw new BadRequestException(BuildFollowOnlyTagVisibilityDeniedMessage(disallowedTagIds, accountMap));
            }
        }

        private static string BuildTagPermissionDeniedMessage(IEnumerable<Account> disallowedAccounts)
        {
            var usernameSummary = BuildTaggedUsernameSummary(
                disallowedAccounts.Select(x => x.Username));

            if (string.IsNullOrWhiteSpace(usernameSummary))
            {
                return "Some selected users do not allow being tagged.";
            }

            return $"These users do not allow being tagged: {usernameSummary}.";
        }

        private static string BuildFollowOnlyTagVisibilityDeniedMessage(
            IEnumerable<Guid> disallowedTagIds,
            IReadOnlyDictionary<Guid, Account> accountMap)
        {
            var usernameSummary = BuildTaggedUsernameSummary(
                disallowedTagIds
                    .Where(x => accountMap.ContainsKey(x))
                    .Select(x => accountMap[x].Username));

            if (string.IsNullOrWhiteSpace(usernameSummary))
            {
                return "Some selected users cannot be tagged in a Followers-only post.";
            }

            return $"These users cannot be tagged in a Followers-only post because they are not following you: {usernameSummary}.";
        }

        private static string BuildTaggedUsernameSummary(IEnumerable<string?> usernames)
        {
            var normalizedUsernames = (usernames ?? Enumerable.Empty<string?>())
                .Select(x => (x ?? string.Empty).Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (normalizedUsernames.Count == 0)
            {
                return string.Empty;
            }

            var displayedUsernames = normalizedUsernames
                .Take(MaxTagErrorDisplayCount)
                .ToList();
            var remainingCount = normalizedUsernames.Count - displayedUsernames.Count;

            if (remainingCount > 0)
            {
                return $"{string.Join(", ", displayedUsernames)}, and {remainingCount} more";
            }

            return string.Join(", ", displayedUsernames);
        }

        private static List<PostTaggedAccountResponse> MapTaggedAccounts(IEnumerable<Account> accounts)
        {
            return (accounts ?? Enumerable.Empty<Account>())
                .Select(x => new PostTaggedAccountResponse
                {
                    AccountId = x.AccountId,
                    Username = x.Username,
                    FullName = x.FullName,
                    AvatarUrl = x.AvatarUrl,
                    IsFollowing = false,
                    IsFollower = false
                })
                .ToList();
        }
    }
}
