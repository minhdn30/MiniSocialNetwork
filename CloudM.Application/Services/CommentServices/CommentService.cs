using AutoMapper;
using CloudM.Application.DTOs.AccountDTOs;
using CloudM.Application.DTOs.CommentDTOs;
using CloudM.Application.DTOs.CommonDTOs;
using CloudM.Application.Helpers.StoryHelpers;
using CloudM.Application.Helpers.ValidationHelpers;
using CloudM.Domain.Entities;
using CloudM.Domain.Enums;
using CloudM.Domain.Helpers;
using CloudM.Infrastructure.Models;
using CloudM.Infrastructure.Repositories.Accounts;
using CloudM.Infrastructure.Repositories.AccountBlocks;
using CloudM.Infrastructure.Repositories.CommentReacts;
using CloudM.Infrastructure.Repositories.Comments;
using CloudM.Infrastructure.Repositories.Follows;
using CloudM.Infrastructure.Repositories.Posts;
using CloudM.Infrastructure.Repositories.UnitOfWork;
using CloudM.Application.Services.NotificationServices;
using CloudM.Application.Services.RealtimeServices;
using CloudM.Application.Services.StoryViewServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


using static CloudM.Domain.Exceptions.CustomExceptions;

namespace CloudM.Application.Services.CommentServices
{
    public class CommentService : ICommentService
    {
        private readonly ICommentRepository _commentRepository;
        private readonly ICommentReactRepository _commentReactRepository;
        private readonly IPostRepository _postRepository;
        private readonly IAccountRepository _accountRepository;
        private readonly IFollowRepository _followRepository;
        private readonly IMapper _mapper;
        private readonly INotificationService _notificationService;
        private readonly IRealtimeService _realtimeService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IStoryRingStateHelper _storyRingStateHelper;
        private readonly IAccountBlockRepository _accountBlockRepository;

        public CommentService(ICommentRepository commentRepository, ICommentReactRepository commentReactRepository, IPostRepository postRepository,
            IAccountRepository accountRepository, IFollowRepository followRepository, IMapper mapper, INotificationService notificationService, IRealtimeService realtimeService,
            IUnitOfWork unitOfWork, IStoryViewService? storyViewService = null, IStoryRingStateHelper? storyRingStateHelper = null,
            IAccountBlockRepository? accountBlockRepository = null)
        {
            _commentRepository = commentRepository;
            _commentReactRepository = commentReactRepository;
            _postRepository = postRepository;
            _accountRepository = accountRepository;
            _followRepository = followRepository;
            _mapper = mapper;
            _notificationService = notificationService;
            _realtimeService = realtimeService;
            _unitOfWork = unitOfWork;
            _storyRingStateHelper = storyRingStateHelper ?? new StoryRingStateHelper(storyViewService);
            _accountBlockRepository = accountBlockRepository ?? NullAccountBlockRepository.Instance;
        }

        public CommentService(
            ICommentRepository commentRepository,
            ICommentReactRepository commentReactRepository,
            IPostRepository postRepository,
            IAccountRepository accountRepository,
            IFollowRepository followRepository,
            IMapper mapper,
            IRealtimeService realtimeService,
            IUnitOfWork unitOfWork,
            IStoryViewService? storyViewService = null,
            IStoryRingStateHelper? storyRingStateHelper = null)
            : this(
                commentRepository,
                commentReactRepository,
                postRepository,
                accountRepository,
                followRepository,
                mapper,
                NullNotificationService.Instance,
                realtimeService,
                unitOfWork,
                storyViewService,
                storyRingStateHelper)
        {
        }

        public async Task<CommentResponse> AddCommentAsync(Guid postId, Guid accountId, CommentCreateRequest request)
        {
            var post = await _postRepository.GetPostBasicInfoById(postId);
            if (post == null)
            {
                throw new BadRequestException($"Post with ID {postId} not found.");
            }

            await ValidatePostPrivacyAsync(post, accountId, "comment on");
            if (await _accountBlockRepository.IsBlockedEitherWayAsync(accountId, post.AccountId))
                throw new BadRequestException("This content is no longer available.");
 
            var account = await _accountRepository.GetAccountById(accountId);
            if(account == null)
            {
                throw new BadRequestException($"Account with ID {accountId} not found.");
            }

            if (!SocialRoleRules.IsSocialEligible(account))
                throw new ForbiddenException("You must reactivate your account to comment.");
            Comment? parentComment = null;
            if (request.ParentCommentId.HasValue)
            {
                var parentId = request.ParentCommentId.Value;
                parentComment = await _commentRepository.GetCommentById(parentId);
                if (parentComment == null || parentComment.ParentCommentId != null)
                {
                    var message = parentComment == null
                        ? $"Parent comment with ID {parentId} not found."
                        : "Cannot reply to a reply. Only one level of reply is allowed.";

                    throw new BadRequestException(message);
                }

                if (await _accountBlockRepository.IsBlockedEitherWayAsync(accountId, parentComment.AccountId))
                    throw new BadRequestException("This content is no longer available.");
            }

            var comment = _mapper.Map<Comment>(request);
            comment.PostId = postId;
            comment.AccountId = accountId;
            var sanitizeResult = await SanitizeMentionsAsync(comment.Content, post, accountId);
            comment.Content = sanitizeResult.SanitizedContent;

            return await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                await _commentRepository.AddComment(comment);
                await _unitOfWork.CommitAsync(); // Physical save needed for IDs and Counts

                var notificationAt = DateTime.UtcNow;
                await EnqueueCommentCreatedNotificationsAsync(
                    comment,
                    post,
                    parentComment,
                    sanitizeResult.MentionedAccountIds,
                    notificationAt);

                var result = _mapper.Map<CommentResponse>(comment);

                // Populate Owner info for realtime rendering
                if (account != null)
                {
                    result.Owner = _mapper.Map<AccountBasicInfoResponse>(account);
                    result.Owner.StoryRingState = await ResolveStoryRingStateAsync(accountId, accountId);
                }

                result.TotalCommentCount = await _commentRepository.CountCommentsByPostId(postId, accountId);

                // Calculate business rules
                result.CanEdit = true; // Newly created comment by the user
                result.CanDelete = true; // Owner of the comment can always delete it

                // Send realtime notification
                int? parentReplyCount = null;
                if (result.ParentCommentId.HasValue)
                {
                    parentReplyCount = await _commentRepository.CountCommentRepliesAsync(result.ParentCommentId.Value, accountId);
                }
                await _realtimeService.NotifyCommentCreatedAsync(postId, result, parentReplyCount);

                return result;
            });
        }

        public async Task<CommentResponse> UpdateCommentAsync(Guid commentId, Guid accountId, CommentUpdateRequest request)
        {
            var comment = await _commentRepository.GetCommentById(commentId);
            if (comment == null)
            {
                throw new BadRequestException($"Comment with ID {commentId} not found.");
            }

            if (comment.AccountId != accountId)
            {
                throw new ForbiddenException("You are not authorized to update this comment.");
            }

            if (comment.Account.Status != AccountStatusEnum.Active)
                throw new ForbiddenException("You must reactivate your account to update comments.");

            var post = await _postRepository.GetPostBasicInfoById(comment.PostId);
            if (post == null)
            {
                throw new BadRequestException("Post no longer exists.");
            }

            await ValidatePostPrivacyAsync(post, accountId, "modify comments on");

            // Only update content as requested
            var previousMentionAccountIds = ExtractCanonicalMentionAccountIds(comment.Content);
            var sanitizeResult = await SanitizeMentionsAsync(request.Content, post, accountId);
            comment.Content = sanitizeResult.SanitizedContent;
            comment.UpdatedAt = DateTime.UtcNow;

            var addedMentionAccountIds = sanitizeResult.MentionedAccountIds
                .Where(x => !previousMentionAccountIds.Contains(x))
                .Distinct()
                .ToList();
            var removedMentionAccountIds = previousMentionAccountIds
                .Where(x => !sanitizeResult.MentionedAccountIds.Contains(x))
                .Distinct()
                .ToList();

            if (addedMentionAccountIds.Count > 0)
            {
                await EnqueueCommentMentionNotificationsAsync(
                    comment.CommentId,
                    comment.PostId,
                    accountId,
                    addedMentionAccountIds,
                    NotificationAggregateActionEnum.Upsert,
                    DateTime.UtcNow,
                    keepWhenEmpty: false);
            }

            if (removedMentionAccountIds.Count > 0)
            {
                await EnqueueCommentMentionNotificationsAsync(
                    comment.CommentId,
                    comment.PostId,
                    accountId,
                    removedMentionAccountIds,
                    NotificationAggregateActionEnum.Deactivate,
                    DateTime.UtcNow,
                    keepWhenEmpty: false);
            }

            await _commentRepository.UpdateComment(comment);
            await _unitOfWork.CommitAsync();
            
            var result = _mapper.Map<CommentResponse>(comment);
            
            // Populate essential info for realtime rendering (consistent with AddCommentAsync)
            var account = await _accountRepository.GetAccountById(accountId);
            if (account != null)
            {
                result.Owner = _mapper.Map<AccountBasicInfoResponse>(account);
                result.Owner.StoryRingState = await ResolveStoryRingStateAsync(accountId, accountId);
            }

            result.ReactCount = await _commentReactRepository.CountCommentReactAsync(comment.CommentId);
            result.ReplyCount = await _commentRepository.CountCommentRepliesAsync(comment.CommentId, accountId);
            result.TotalCommentCount = await _commentRepository.CountCommentsByPostId(comment.PostId, accountId);
            
            // Calculate business rules
            result.CanEdit = true;
            result.CanDelete = true;

            // Send realtime notification
            await _realtimeService.NotifyCommentUpdatedAsync(comment.PostId, result);

            return result;
        }

        public async Task<CommentDeleteResult> DeleteCommentAsync(Guid commentId,  Guid accountId, bool isAdmin)
        {
            var comment = await _commentRepository.GetCommentById(commentId);
            if (comment == null)
            {
                throw new BadRequestException($"Comment with ID {commentId} not found.");
            }
            var post = await _postRepository.GetPostBasicInfoById(comment.PostId);
            if (post == null)
            {
                throw new BadRequestException($"Post with ID {comment.PostId} not found.");
            }

            bool isPostOwner = post.AccountId == accountId;

            if (comment.AccountId != accountId && !isPostOwner && !isAdmin)
            {
                throw new ForbiddenException("You are not authorized to delete this comment.");
            }

            if (!isAdmin && comment.Account.Status != AccountStatusEnum.Active && !isPostOwner)
                throw new ForbiddenException("You must reactivate your account to delete comments.");

            if (!isAdmin && !isPostOwner)
            {
                await ValidatePostPrivacyAsync(post, accountId, "manage comments on");
            }

            var postId = comment.PostId;
            var parentId = comment.ParentCommentId;
            var commentThread = await _commentRepository.GetCommentThreadForDeleteAsync(commentId) ?? new List<Comment>();
            if (commentThread.Count == 0)
            {
                commentThread = new List<Comment> { comment };
            }

            var threadOwnerMap = commentThread
                .GroupBy(x => x.CommentId)
                .ToDictionary(x => x.Key, x => x.First().AccountId);
            var parentOwnerMap = new Dictionary<Guid, Guid>();
            var parentIds = commentThread
                .Where(x => x.ParentCommentId.HasValue)
                .Select(x => x.ParentCommentId!.Value)
                .Distinct()
                .ToList();
            foreach (var parentCommentId in parentIds)
            {
                if (threadOwnerMap.TryGetValue(parentCommentId, out var ownerInThread))
                {
                    parentOwnerMap[parentCommentId] = ownerInThread;
                    continue;
                }

                var parentComment = await _commentRepository.GetCommentById(parentCommentId);
                if (parentComment != null)
                {
                    parentOwnerMap[parentCommentId] = parentComment.AccountId;
                }
            }

            return await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                await _commentRepository.DeleteCommentWithReplies(commentId);
                
                // Commit changes first
                await _unitOfWork.CommitAsync();

                var notificationAt = DateTime.UtcNow;
                foreach (var deletedComment in commentThread)
                {
                    if (deletedComment.ParentCommentId.HasValue)
                    {
                        var deletedParentId = deletedComment.ParentCommentId.Value;
                        if (parentOwnerMap.TryGetValue(deletedParentId, out var parentOwnerId) &&
                            parentOwnerId != Guid.Empty &&
                            parentOwnerId != deletedComment.AccountId)
                        {
                            await _notificationService.EnqueueAggregateEventAsync(new NotificationAggregateEvent
                            {
                                RecipientId = parentOwnerId,
                                Action = NotificationAggregateActionEnum.Deactivate,
                                Type = NotificationTypeEnum.CommentReply,
                                AggregateKey = NotificationAggregateKeys.CommentReply(deletedParentId),
                                SourceType = NotificationSourceTypeEnum.Reply,
                                SourceId = deletedComment.CommentId,
                                ActorId = deletedComment.AccountId,
                                TargetKind = NotificationTargetKindEnum.Post,
                                TargetId = deletedComment.PostId,
                                KeepWhenEmpty = true,
                                OccurredAt = notificationAt
                            });
                        }
                    }
                    else if (post.AccountId != deletedComment.AccountId)
                    {
                        await _notificationService.EnqueueAggregateEventAsync(new NotificationAggregateEvent
                        {
                            RecipientId = post.AccountId,
                            Action = NotificationAggregateActionEnum.Deactivate,
                            Type = NotificationTypeEnum.PostComment,
                            AggregateKey = NotificationAggregateKeys.PostComment(deletedComment.PostId),
                            SourceType = NotificationSourceTypeEnum.Comment,
                            SourceId = deletedComment.CommentId,
                            ActorId = deletedComment.AccountId,
                            TargetKind = NotificationTargetKindEnum.Post,
                            TargetId = deletedComment.PostId,
                            KeepWhenEmpty = true,
                            OccurredAt = notificationAt
                        });
                    }

                    var deletedMentionRecipients = ExtractCanonicalMentionAccountIds(deletedComment.Content);
                    if (deletedMentionRecipients.Count > 0)
                    {
                        await EnqueueCommentMentionNotificationsAsync(
                            deletedComment.CommentId,
                            deletedComment.PostId,
                            deletedComment.AccountId,
                            deletedMentionRecipients,
                            NotificationAggregateActionEnum.Deactivate,
                            notificationAt,
                            keepWhenEmpty: true);
                    }
                    
                    await EnqueueCommentReactDeactivateAllAsync(
                        deletedComment,
                        notificationAt);
                }

                // Get updated counts logic
                int? totalComments = null;
                int? parentReplyCount = null;

                if (parentId.HasValue)
                {
                    parentReplyCount = await _commentRepository.CountCommentRepliesAsync(parentId.Value, accountId);
                }
                else
                {
                    totalComments = await _commentRepository.CountCommentsByPostId(postId, accountId);
                }

                var deleteResult = new CommentDeleteResult
                {
                    PostId = postId,
                    ParentCommentId = parentId,
                    TotalPostComments = totalComments,
                    ParentReplyCount = parentReplyCount
                };

                // Send realtime notification
                await _realtimeService.NotifyCommentDeletedAsync(postId, commentId, parentId, totalComments, parentReplyCount);

                return deleteResult;
            });
        }

        public async Task<CommentCursorResponse> GetCommentsByPostIdAsync(Guid postId, Guid? currentId, DateTime? cursorCreatedAt, Guid? cursorCommentId, int pageSize, Guid? priorityCommentId = null)
        {
            var post = await _postRepository.GetPostBasicInfoById(postId);
            if (post == null)
                throw new BadRequestException($"Post with ID {postId} not found.");

            await ValidatePostPrivacyAsync(post, currentId, "view comments on");

            var (items, totalItems, nextCursorCreatedAt, nextCursorCommentId) = await _commentRepository.GetCommentsByPostIdWithReplyCountAsync(postId, currentId, cursorCreatedAt, cursorCommentId, pageSize, priorityCommentId);

            var responseItems = items.Select(item =>
            {
                var response = _mapper.Map<CommentResponse>(item);
                response.CanEdit = currentId != null && item.Owner.AccountId == currentId;
                response.CanDelete = currentId != null && (item.Owner.AccountId == currentId || item.PostOwnerId == currentId);
                return response;
            }).ToList();

            await ApplyStoryRingStatesForCommentOwnersAsync(currentId, responseItems);

            return new CommentCursorResponse
            {
                Items = responseItems,
                TotalCount = totalItems,
                NextCursor = nextCursorCreatedAt.HasValue && nextCursorCommentId.HasValue
                    ? new CommentNextCursorResponse
                    {
                        CreatedAt = nextCursorCreatedAt.Value,
                        CommentId = nextCursorCommentId.Value
                    }
                    : null
            };
        }
        public async Task<CommentCursorResponse> GetRepliesByCommentIdAsync(Guid commentId, Guid? currentId, DateTime? cursorCreatedAt, Guid? cursorCommentId, int pageSize, Guid? priorityReplyId = null)
        {
            var comment = await _commentRepository.GetCommentById(commentId);
            if (comment == null)
                throw new BadRequestException($"Comment with ID {commentId} not found.");

            var post = await _postRepository.GetPostBasicInfoById(comment.PostId);
            if (post == null)
                throw new BadRequestException("Post no longer exists.");

            await ValidatePostPrivacyAsync(post, currentId, "view replies on");

            var (items, totalItems, nextCursorCreatedAt, nextCursorCommentId) = await _commentRepository.GetRepliesByCommentIdAsync(commentId, currentId, cursorCreatedAt, cursorCommentId, pageSize, priorityReplyId);

            var responseItems = items.Select(item =>
            {
                var response = _mapper.Map<CommentResponse>(item);
                response.CanEdit = currentId != null && item.Owner.AccountId == currentId;
                response.CanDelete = currentId != null && (item.Owner.AccountId == currentId || item.PostOwnerId == currentId);
                return response;
            }).ToList();

            await ApplyStoryRingStatesForCommentOwnersAsync(currentId, responseItems);

            return new CommentCursorResponse
            {
                Items = responseItems,
                TotalCount = totalItems,
                NextCursor = nextCursorCreatedAt.HasValue && nextCursorCommentId.HasValue
                    ? new CommentNextCursorResponse
                    {
                        CreatedAt = nextCursorCreatedAt.Value,
                        CommentId = nextCursorCommentId.Value
                    }
                    : null
            };
        }
        public async Task<CommentResponse?> GetCommentByIdAsync(Guid commentId)
        {
            var comment = await _commentRepository.GetCommentById(commentId);
            return comment != null ? _mapper.Map<CommentResponse>(comment) : null;
        }

        public async Task<int> GetReplyCountAsync(Guid commentId)
        {
            return await _commentRepository.CountCommentRepliesAsync(commentId);
        }

        private async Task ValidatePostPrivacyAsync(Post post, Guid? currentId, string action)
        {
            if (post.Privacy == PostPrivacyEnum.Private)
            {
                if (currentId == null || currentId != post.AccountId)
                    throw new ForbiddenException($"Only the post owner can {action} a private post.");
            }
            else if (post.Privacy == PostPrivacyEnum.FollowOnly)
            {
                if (currentId == null || (currentId != post.AccountId && !await _followRepository.IsFollowingAsync(currentId.Value, post.AccountId)))
                    throw new ForbiddenException($"Only followers can {action} this post.");
            }
        }

        private async Task ApplyStoryRingStatesForCommentOwnersAsync(Guid? currentId, List<CommentResponse> comments)
        {
            if (!currentId.HasValue || comments.Count == 0)
            {
                return;
            }

            var ownerIds = comments
                .Select(x => x.Owner?.AccountId ?? Guid.Empty)
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList();

            if (ownerIds.Count == 0)
            {
                return;
            }

            var stateMap = await _storyRingStateHelper.ResolveManyAsync(currentId.Value, ownerIds);

            foreach (var comment in comments)
            {
                if (comment.Owner == null)
                {
                    continue;
                }

                comment.Owner.StoryRingState = stateMap.TryGetValue(comment.Owner.AccountId, out var ringState)
                    ? ringState
                    : StoryRingStateEnum.None;
            }
        }

        private async Task<StoryRingStateEnum> ResolveStoryRingStateAsync(Guid currentId, Guid targetAccountId)
        {
            return await _storyRingStateHelper.ResolveAsync(currentId, targetAccountId);
        }

        private async Task<CommentMentionSanitizeResult> SanitizeMentionsAsync(string? content, Post post, Guid currentId)
        {
            var safeContent = content ?? string.Empty;
            var mentionTokens = MentionParser.ExtractTokens(safeContent);
            if (mentionTokens.Count == 0)
            {
                return new CommentMentionSanitizeResult
                {
                    SanitizedContent = safeContent
                };
            }

            var canonicalMentionAccountIds = mentionTokens
                .Where(x => x.IsCanonical && x.AccountId.HasValue)
                .Select(x => x.AccountId!.Value)
                .Distinct()
                .ToList();

            var plainMentionUsernames = mentionTokens
                .Where(x => !x.IsCanonical)
                .Select(x => MentionParser.NormalizeUsername(x.Username))
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var accountsByIds = canonicalMentionAccountIds.Count > 0
                ? await _accountRepository.GetAccountsByIds(canonicalMentionAccountIds)
                : new List<Account>();
            var accountById = accountsByIds
                .Where(x => x.Status == AccountStatusEnum.Active)
                .ToDictionary(x => x.AccountId, x => x);

            var accountByUsername = (await _accountRepository.GetAccountsByUsernames(plainMentionUsernames))
                .Where(x => x.Status == AccountStatusEnum.Active)
                .GroupBy(x => (x.Username ?? string.Empty).Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            var candidateAccounts = accountById.Values
                .Concat(accountByUsername.Values)
                .GroupBy(x => x.AccountId)
                .Select(g => g.First())
                .ToList();

            var blockedAccountIds = (await _accountBlockRepository.GetRelationsAsync(
                    currentId,
                    candidateAccounts.Select(x => x.AccountId)))
                .Where(x => x.IsBlockedEitherWay)
                .Select(x => x.TargetId)
                .ToHashSet();

            if (blockedAccountIds.Count > 0)
            {
                accountById = accountById
                    .Where(x => !blockedAccountIds.Contains(x.Key))
                    .ToDictionary(x => x.Key, x => x.Value);

                accountByUsername = accountByUsername
                    .Where(x => !blockedAccountIds.Contains(x.Value.AccountId))
                    .ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);

                candidateAccounts = candidateAccounts
                    .Where(x => !blockedAccountIds.Contains(x.AccountId))
                    .ToList();
            }

            var visibilityCandidateIds = candidateAccounts
                .Where(x => x.AccountId != post.AccountId)
                .Select(x => x.AccountId)
                .Distinct()
                .ToList();

            HashSet<Guid> followOnlyVisibleAccountIds = new();
            if (post.Privacy == PostPrivacyEnum.FollowOnly && visibilityCandidateIds.Count > 0)
            {
                followOnlyVisibleAccountIds = await _followRepository.GetFollowerIdsInTargetsAsync(
                    post.AccountId,
                    visibilityCandidateIds);
            }

            var builder = new StringBuilder();
            var currentIndex = 0;
            var mentionedAccountIds = new HashSet<Guid>();
            foreach (var token in mentionTokens)
            {
                if (token.StartIndex > currentIndex)
                {
                    builder.Append(safeContent.AsSpan(currentIndex, token.StartIndex - currentIndex));
                }

                var resolvedAccount = ResolveMentionTargetAccount(token, accountById, accountByUsername);
                if (resolvedAccount != null && IsMentionAllowedByBusinessRules(resolvedAccount, post, followOnlyVisibleAccountIds))
                {
                    builder.Append(MentionParser.BuildCanonicalMentionText(resolvedAccount.Username, resolvedAccount.AccountId));
                    mentionedAccountIds.Add(resolvedAccount.AccountId);
                }
                else
                {
                    builder.Append(MentionParser.BuildPlainMentionText(token.Username));
                }

                currentIndex = token.StartIndex + token.Length;
            }

            if (currentIndex < safeContent.Length)
            {
                builder.Append(safeContent.AsSpan(currentIndex));
            }

            return new CommentMentionSanitizeResult
            {
                SanitizedContent = builder.ToString(),
                MentionedAccountIds = mentionedAccountIds
            };
        }

        private static Account? ResolveMentionTargetAccount(
            MentionParser.MentionToken token,
            IReadOnlyDictionary<Guid, Account> accountById,
            IReadOnlyDictionary<string, Account> accountByUsername)
        {
            if (token.IsCanonical && token.AccountId.HasValue && accountById.TryGetValue(token.AccountId.Value, out var accountByTokenId))
            {
                return accountByTokenId;
            }

            var normalizedUsername = MentionParser.NormalizeUsername(token.Username);
            if (string.IsNullOrWhiteSpace(normalizedUsername))
            {
                return null;
            }

            return accountByUsername.TryGetValue(normalizedUsername, out var accountByTokenUsername)
                ? accountByTokenUsername
                : null;
        }

        private static bool IsMentionAllowedByBusinessRules(
            Account targetAccount,
            Post post,
            IReadOnlySet<Guid> followOnlyVisibleAccountIds)
        {
            if (targetAccount.Settings?.TagPermission == TagPermissionEnum.NoOne)
            {
                return false;
            }

            if (targetAccount.AccountId == post.AccountId)
            {
                return true;
            }

            if (post.Privacy == PostPrivacyEnum.Public)
            {
                return true;
            }

            if (post.Privacy == PostPrivacyEnum.FollowOnly)
            {
                return followOnlyVisibleAccountIds.Contains(targetAccount.AccountId);
            }

            return false;
        }

        private async Task EnqueueCommentCreatedNotificationsAsync(
            Comment comment,
            Post post,
            Comment? parentComment,
            IReadOnlyCollection<Guid> mentionedAccountIds,
            DateTime occurredAt)
        {
            if (comment.ParentCommentId.HasValue)
            {
                var parentOwnerId = parentComment?.AccountId ?? Guid.Empty;
                if (parentOwnerId != Guid.Empty && parentOwnerId != comment.AccountId)
                {
                    await _notificationService.EnqueueAggregateEventAsync(new NotificationAggregateEvent
                    {
                        RecipientId = parentOwnerId,
                        Action = NotificationAggregateActionEnum.Upsert,
                        Type = NotificationTypeEnum.CommentReply,
                        AggregateKey = NotificationAggregateKeys.CommentReply(comment.ParentCommentId.Value),
                        SourceType = NotificationSourceTypeEnum.Reply,
                        SourceId = comment.CommentId,
                        ActorId = comment.AccountId,
                        TargetKind = NotificationTargetKindEnum.Post,
                        TargetId = comment.PostId,
                        KeepWhenEmpty = true,
                        OccurredAt = occurredAt
                    });
                }
            }
            else if (post.AccountId != comment.AccountId)
            {
                await _notificationService.EnqueueAggregateEventAsync(new NotificationAggregateEvent
                {
                    RecipientId = post.AccountId,
                    Action = NotificationAggregateActionEnum.Upsert,
                    Type = NotificationTypeEnum.PostComment,
                    AggregateKey = NotificationAggregateKeys.PostComment(comment.PostId),
                    SourceType = NotificationSourceTypeEnum.Comment,
                    SourceId = comment.CommentId,
                    ActorId = comment.AccountId,
                    TargetKind = NotificationTargetKindEnum.Post,
                    TargetId = comment.PostId,
                    KeepWhenEmpty = true,
                    OccurredAt = occurredAt
                });
            }

            if (mentionedAccountIds.Count > 0)
            {
                await EnqueueCommentMentionNotificationsAsync(
                    comment.CommentId,
                    comment.PostId,
                    comment.AccountId,
                    mentionedAccountIds,
                    NotificationAggregateActionEnum.Upsert,
                    occurredAt,
                    keepWhenEmpty: false);
            }
        }

        private async Task EnqueueCommentMentionNotificationsAsync(
            Guid commentId,
            Guid postId,
            Guid actorId,
            IEnumerable<Guid> recipients,
            NotificationAggregateActionEnum action,
            DateTime occurredAt,
            bool keepWhenEmpty)
        {
            var normalizedRecipients = (recipients ?? Enumerable.Empty<Guid>())
                .Where(x => x != Guid.Empty && x != actorId)
                .Distinct()
                .ToList();
            if (normalizedRecipients.Count == 0)
            {
                return;
            }

            foreach (var recipientId in normalizedRecipients)
            {
                await _notificationService.EnqueueAggregateEventAsync(new NotificationAggregateEvent
                {
                    RecipientId = recipientId,
                    Action = action,
                    Type = NotificationTypeEnum.CommentMention,
                    AggregateKey = NotificationAggregateKeys.CommentMention(commentId),
                    SourceType = NotificationSourceTypeEnum.Mention,
                    SourceId = commentId,
                    ActorId = actorId,
                    TargetKind = NotificationTargetKindEnum.Post,
                    TargetId = postId,
                    KeepWhenEmpty = keepWhenEmpty,
                    OccurredAt = occurredAt
                });
            }
        }

        private async Task EnqueueCommentReactDeactivateAllAsync(
            Comment comment,
            DateTime occurredAt)
        {
            if (comment.AccountId == Guid.Empty || comment.PostId == Guid.Empty)
            {
                return;
            }

            var isReply = comment.ParentCommentId.HasValue;
            await _notificationService.EnqueueAggregateEventAsync(new NotificationAggregateEvent
            {
                RecipientId = comment.AccountId,
                Action = NotificationAggregateActionEnum.DeactivateAll,
                Type = isReply ? NotificationTypeEnum.ReplyReact : NotificationTypeEnum.CommentReact,
                AggregateKey = isReply
                    ? NotificationAggregateKeys.ReplyReact(comment.CommentId)
                    : NotificationAggregateKeys.CommentReact(comment.CommentId),
                SourceType = isReply
                    ? NotificationSourceTypeEnum.ReplyReact
                    : NotificationSourceTypeEnum.CommentReact,
                SourceId = Guid.Empty,
                ActorId = null,
                TargetKind = NotificationTargetKindEnum.Post,
                TargetId = comment.PostId,
                KeepWhenEmpty = false,
                OccurredAt = occurredAt
            });
        }

        private static HashSet<Guid> ExtractCanonicalMentionAccountIds(string? content)
        {
            return MentionParser.ExtractTokens(content)
                .Where(x => x.IsCanonical && x.AccountId.HasValue && x.AccountId.Value != Guid.Empty)
                .Select(x => x.AccountId!.Value)
                .ToHashSet();
        }

        private sealed class CommentMentionSanitizeResult
        {
            public string SanitizedContent { get; set; } = string.Empty;
            public HashSet<Guid> MentionedAccountIds { get; set; } = new();
        }
    }
}
