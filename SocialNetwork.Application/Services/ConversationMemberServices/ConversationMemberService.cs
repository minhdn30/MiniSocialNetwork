using AutoMapper;
using SocialNetwork.Application.DTOs.AccountDTOs;
using SocialNetwork.Application.DTOs.ConversationDTOs;
using SocialNetwork.Application.DTOs.ConversationMemberDTOs;
using SocialNetwork.Application.DTOs.MessageDTOs;
using SocialNetwork.Application.Services.RealtimeServices;
using SocialNetwork.Domain.Entities;
using SocialNetwork.Domain.Enums;
using SocialNetwork.Infrastructure.Repositories.Accounts;
using SocialNetwork.Infrastructure.Repositories.ConversationMembers;
using SocialNetwork.Infrastructure.Repositories.Conversations;
using SocialNetwork.Infrastructure.Repositories.Follows;
using SocialNetwork.Infrastructure.Repositories.Messages;
using SocialNetwork.Infrastructure.Repositories.UnitOfWork;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using static SocialNetwork.Domain.Exceptions.CustomExceptions;

namespace SocialNetwork.Application.Services.ConversationMemberServices
{
    public class ConversationMemberService : IConversationMemberService
    {
        private const int MaxConversationThemeLength = 32;
        private const int MaxGroupConversationMembers = 50;
        private readonly IConversationRepository _conversationRepository;
        private readonly IConversationMemberRepository _conversationMemberRepository;
        private readonly IAccountRepository _accountRepository;
        private readonly IFollowRepository _followRepository;
        private readonly IMessageRepository _messageRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly IRealtimeService _realtimeService;
        public ConversationMemberService(IConversationRepository conversationRepository, IConversationMemberRepository conversationMemberRepository,
            IAccountRepository accountRepository, IFollowRepository followRepository, IMapper mapper, IMessageRepository messageRepository,
            IUnitOfWork unitOfWork, IRealtimeService realtimeService)
        {
            _conversationRepository = conversationRepository;
            _conversationMemberRepository = conversationMemberRepository;
            _accountRepository = accountRepository;
            _followRepository = followRepository;
            _messageRepository = messageRepository;
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _realtimeService = realtimeService;
        }
        public async Task SetMuteStatusAsync(Guid conversationId, Guid currentId, bool isMuted)
        {
            var member = await _conversationMemberRepository.GetConversationMemberAsync(conversationId, currentId);
            if (member == null)
                throw new ForbiddenException($"Account with ID {currentId} is not a member of this conversation.");

            if (member.IsMuted != isMuted)
            {
                member.IsMuted = isMuted;
                await _conversationMemberRepository.UpdateConversationMember(member);
                await _unitOfWork.CommitAsync();
            }

            await _realtimeService.NotifyConversationMuteUpdatedAsync(currentId, conversationId, isMuted);
        }

        public async Task SetThemeAsync(Guid conversationId, Guid currentId, ConversationThemeUpdateRequest request)
        {
            if (!await _conversationMemberRepository.IsMemberOfConversation(conversationId, currentId))
                throw new ForbiddenException("You are not a member of this conversation.");

            var conversation = await _conversationRepository.GetConversationByIdAsync(conversationId);
            if (conversation == null)
                throw new NotFoundException($"Conversation with ID {conversationId} does not exist.");

            var actor = await _accountRepository.GetAccountById(currentId);
            if (actor == null)
                throw new NotFoundException($"Account with ID {currentId} does not exist.");

            var oldTheme = conversation.Theme;
            var normalizedTheme = NormalizeTheme(request?.Theme);
            if (string.Equals(conversation.Theme, normalizedTheme, StringComparison.Ordinal))
                return;

            conversation.Theme = normalizedTheme;
            await _conversationRepository.UpdateConversationAsync(conversation);
            var systemMessage = new Message
            {
                ConversationId = conversationId,
                AccountId = currentId,
                MessageType = MessageTypeEnum.System,
                Content = BuildThemeSystemMessageFallback(actor.Username, normalizedTheme),
                SystemMessageDataJson = JsonSerializer.Serialize(new
                {
                    action = (int)SystemMessageActionEnum.ConversationThemeUpdated,
                    actorAccountId = currentId,
                    actorUsername = actor.Username,
                    theme = normalizedTheme,
                    previousTheme = oldTheme
                }),
                SentAt = DateTime.UtcNow,
                IsEdited = false,
                IsRecalled = false
            };
            await _messageRepository.AddMessageAsync(systemMessage);
            await _unitOfWork.CommitAsync();

            await _realtimeService.NotifyConversationThemeUpdatedAsync(conversationId, normalizedTheme, currentId);

            var realtimeMessage = _mapper.Map<SendMessageResponse>(systemMessage);
            realtimeMessage.Sender = _mapper.Map<AccountChatInfoResponse>(actor);
            var muteMap = await _conversationMemberRepository.GetMembersWithMuteStatusAsync(conversationId);
            await _realtimeService.NotifyNewMessageAsync(conversationId, muteMap, realtimeMessage);
        }

        public async Task UpdateMemberNickname(Guid conversationId, Guid currentId, ConversationMemberNicknameUpdateRequest request)
        {
            if(! await _conversationMemberRepository.IsMemberOfConversation(conversationId, currentId))
                throw new ForbiddenException("You are not a member of this conversation.");

            var member = await _conversationMemberRepository.GetConversationMemberAsync(conversationId, request.AccountId);
            if (member == null)
                throw new BadRequestException($"Account with ID {request.AccountId} is not a member of this conversation.");

            var actor = await _accountRepository.GetAccountById(currentId);
            if (actor == null)
                throw new NotFoundException($"Account with ID {currentId} does not exist.");

            var target = request.AccountId == currentId ? actor : await _accountRepository.GetAccountById(request.AccountId);
            if (target == null)
                throw new NotFoundException($"Account with ID {request.AccountId} does not exist.");

            var oldNickname = member.Nickname;
            var trimmed = request.Nickname?.Trim();
            member.Nickname = string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;

            var nicknameChanged = !string.Equals(oldNickname, member.Nickname, StringComparison.Ordinal);
            if (!nicknameChanged)
                return;

            await _conversationMemberRepository.UpdateConversationMember(member);
            var systemMessage = new Message
            {
                ConversationId = conversationId,
                AccountId = currentId,
                MessageType = MessageTypeEnum.System,
                Content = BuildNicknameSystemMessageFallback(actor.Username, target.Username, member.Nickname),
                SystemMessageDataJson = JsonSerializer.Serialize(new
                {
                    action = (int)SystemMessageActionEnum.MemberNicknameUpdated,
                    actorAccountId = currentId,
                    actorUsername = actor.Username,
                    targetAccountId = request.AccountId,
                    targetUsername = target.Username,
                    nickname = member.Nickname,
                    previousNickname = oldNickname
                }),
                SentAt = DateTime.UtcNow,
                IsEdited = false,
                IsRecalled = false
            };
            await _messageRepository.AddMessageAsync(systemMessage);
            await _unitOfWork.CommitAsync();

            var memberIds = await _conversationMemberRepository.GetMemberIdsByConversationIdAsync(conversationId);
            await _realtimeService.NotifyConversationNicknameUpdatedAsync(
                conversationId,
                request.AccountId,
                member.Nickname,
                currentId,
                memberIds);

            var realtimeMessage = _mapper.Map<SendMessageResponse>(systemMessage);
            realtimeMessage.Sender = _mapper.Map<AccountChatInfoResponse>(actor);
            var muteMap = await _conversationMemberRepository.GetMembersWithMuteStatusAsync(conversationId);
            await _realtimeService.NotifyNewMessageAsync(conversationId, muteMap, realtimeMessage);
        }

        public async Task<List<GroupInviteAccountSearchResponse>> SearchAccountsForGroupInviteAsync(
            Guid currentId,
            string keyword,
            IEnumerable<Guid>? excludeAccountIds,
            int limit = 10)
        {
            var normalizedKeyword = keyword?.Trim() ?? string.Empty;
            if (normalizedKeyword.Length > 0 && normalizedKeyword.Length < 2)
                throw new BadRequestException("Keyword must be empty or at least 2 characters.");

            var results = await _accountRepository.SearchAccountsForGroupInviteAsync(
                currentId,
                normalizedKeyword,
                excludeAccountIds,
                limit);

            return results.Select(x => new GroupInviteAccountSearchResponse
            {
                AccountId = x.AccountId,
                Username = x.Username,
                FullName = x.FullName,
                AvatarUrl = x.AvatarUrl,
                IsFollowing = x.IsFollowing,
                IsFollower = x.IsFollower,
                MutualGroupCount = x.MutualGroupCount,
                LastDirectMessageAt = x.LastDirectMessageAt,
                MatchScore = x.MatchScore,
                FollowingScore = x.FollowingScore,
                FollowerScore = x.FollowerScore,
                RecentChatScore = x.RecentChatScore,
                MutualGroupScore = x.MutualGroupScore,
                TotalScore = x.TotalScore
            }).ToList();
        }

        public async Task<List<GroupInviteAccountSearchResponse>> SearchAccountsForAddGroupMembersAsync(
            Guid conversationId,
            Guid currentId,
            string keyword,
            IEnumerable<Guid>? excludeAccountIds,
            int limit = 10)
        {
            var normalizedKeyword = keyword?.Trim() ?? string.Empty;
            if (normalizedKeyword.Length > 0 && normalizedKeyword.Length < 2)
                throw new BadRequestException("Keyword must be empty or at least 2 characters.");

            var conversation = await _conversationRepository.GetConversationByIdAsync(conversationId);
            if (conversation == null)
                throw new NotFoundException($"Conversation with ID {conversationId} does not exist.");

            if (!conversation.IsGroup)
                throw new BadRequestException("Add member search is only available for group conversations.");

            var actorMember = await _conversationMemberRepository.GetConversationMemberAsync(conversationId, currentId);
            if (actorMember == null)
                throw new ForbiddenException("You are not a member of this conversation.");

            var ownerId = ResolveGroupOwnerId(conversation);
            var actorIsOwner = ownerId.HasValue && ownerId.Value == currentId;
            if (!actorMember.IsAdmin && !actorIsOwner)
                throw new ForbiddenException("Only group admins can search and add members.");

            var activeMemberIds = await _conversationMemberRepository.GetMemberIdsByConversationIdAsync(conversationId);
            if (activeMemberIds.Count >= MaxGroupConversationMembers)
                return new List<GroupInviteAccountSearchResponse>();

            var excludedIds = (excludeAccountIds ?? Enumerable.Empty<Guid>())
                .Where(id => id != Guid.Empty)
                .ToHashSet();

            foreach (var memberId in activeMemberIds)
            {
                excludedIds.Add(memberId);
            }

            return await SearchAccountsForGroupInviteAsync(
                currentId,
                normalizedKeyword,
                excludedIds,
                limit);
        }

        public async Task AddGroupMembersAsync(Guid conversationId, Guid currentId, AddGroupMembersRequest request)
        {
            if (request == null)
                throw new BadRequestException("Request is required.");

            var requestedMemberIds = (request.MemberIds ?? new List<Guid>())
                .Where(id => id != Guid.Empty && id != currentId)
                .Distinct()
                .ToList();

            if (requestedMemberIds.Count == 0)
                throw new BadRequestException("Please select at least one member to add.");

            var conversation = await _conversationRepository.GetConversationByIdAsync(conversationId);
            if (conversation == null)
                throw new NotFoundException($"Conversation with ID {conversationId} does not exist.");

            if (!conversation.IsGroup)
                throw new BadRequestException("Add member is only available for group conversations.");

            var actorMember = await _conversationMemberRepository.GetConversationMemberAsync(conversationId, currentId);
            if (actorMember == null)
                throw new ForbiddenException("You are not a member of this conversation.");

            var ownerId = ResolveGroupOwnerId(conversation);
            var actorIsOwner = ownerId.HasValue && ownerId.Value == currentId;
            if (!actorMember.IsAdmin && !actorIsOwner)
                throw new ForbiddenException("Only group admins can add members.");

            var activeMemberIds = await _conversationMemberRepository.GetMemberIdsByConversationIdAsync(conversationId);
            if (activeMemberIds.Count >= MaxGroupConversationMembers)
                throw new BadRequestException($"A group can contain at most {MaxGroupConversationMembers} members.");

            var activeMemberIdSet = activeMemberIds.ToHashSet();

            var alreadyActiveIds = requestedMemberIds
                .Where(activeMemberIdSet.Contains)
                .ToHashSet();

            if (alreadyActiveIds.Count > 0)
            {
                throw new BadRequestException("One or more selected users are already members of this group.");
            }

            if (activeMemberIds.Count + requestedMemberIds.Count > MaxGroupConversationMembers)
                throw new BadRequestException($"A group can contain at most {MaxGroupConversationMembers} members.");

            var allAccountIds = requestedMemberIds
                .Append(currentId)
                .Distinct()
                .ToList();
            var allAccounts = await _accountRepository.GetAccountsByIds(allAccountIds);
            var actor = allAccounts.FirstOrDefault(account => account.AccountId == currentId);
            if (actor == null)
                throw new NotFoundException($"Account with ID {currentId} does not exist.");

            if (actor.Status != AccountStatusEnum.Active)
                throw new ForbiddenException("You must reactivate your account to add members.");

            var targetAccounts = allAccounts
                .Where(account => account.AccountId != currentId)
                .ToList();

            if (targetAccounts.Count != requestedMemberIds.Count)
                throw new BadRequestException("One or more selected members do not exist.");

            var targetAccountById = targetAccounts.ToDictionary(account => account.AccountId, account => account);
            var orderedTargets = requestedMemberIds
                .Where(targetAccountById.ContainsKey)
                .Select(id => targetAccountById[id])
                .ToList();

            var inactiveAccounts = orderedTargets
                .Where(account => account.Status != AccountStatusEnum.Active)
                .ToList();

            if (inactiveAccounts.Count > 0)
            {
                var inactiveMentions = string.Join(", ", inactiveAccounts
                    .Select(account => ToMention(account.Username))
                    .Distinct()
                    .Take(5));
                var suffix = inactiveAccounts.Count > 5 ? " ..." : string.Empty;
                throw new BadRequestException($"These members are not active: {inactiveMentions}{suffix}");
            }

            var connectedAccountIds = await _followRepository.GetConnectedAccountIdsAsync(currentId, requestedMemberIds);

            var permissionDeniedTargets = orderedTargets
                .Where(target =>
                {
                    var permission = target.Settings?.GroupChatInvitePermission ?? GroupChatInvitePermissionEnum.Anyone;
                    if (permission == GroupChatInvitePermissionEnum.Anyone)
                    {
                        return false;
                    }

                    if (permission == GroupChatInvitePermissionEnum.NoOne)
                    {
                        return true;
                    }

                    return !connectedAccountIds.Contains(target.AccountId);
                })
                .ToList();

            if (permissionDeniedTargets.Count > 0)
            {
                var deniedMentions = string.Join(", ", permissionDeniedTargets
                    .Select(account => ToMention(account.Username))
                    .Distinct()
                    .Take(5));
                var suffix = permissionDeniedTargets.Count > 5 ? " ..." : string.Empty;
                throw new BadRequestException($"You are not allowed to add these members due to invite privacy: {deniedMentions}{suffix}");
            }

            var existingMembers = await _conversationMemberRepository.GetConversationMembersByAccountIdsAsync(conversationId, requestedMemberIds);
            var existingMemberMap = existingMembers.ToDictionary(member => member.AccountId, member => member);

            var nowUtc = DateTime.UtcNow;
            var membersToInsert = new List<ConversationMember>();
            var membersToReactivate = new List<ConversationMember>();
            var addedTargetIds = new HashSet<Guid>();

            foreach (var target in orderedTargets)
            {
                if (!existingMemberMap.TryGetValue(target.AccountId, out var existingMember))
                {
                    membersToInsert.Add(new ConversationMember
                    {
                        ConversationId = conversationId,
                        AccountId = target.AccountId,
                        IsAdmin = false,
                        HasLeft = false,
                        JoinedAt = nowUtc
                    });
                    addedTargetIds.Add(target.AccountId);
                    continue;
                }

                if (!existingMember.HasLeft)
                {
                    continue;
                }

                existingMember.HasLeft = false;
                existingMember.IsAdmin = false;
                existingMember.IsDeleted = false;
                existingMember.IsMuted = false;
                existingMember.ClearedAt = null;
                existingMember.LastSeenAt = null;
                existingMember.LastSeenMessageId = null;
                existingMember.JoinedAt = nowUtc;
                membersToReactivate.Add(existingMember);
                addedTargetIds.Add(target.AccountId);
            }

            if (addedTargetIds.Count == 0)
                throw new BadRequestException("No eligible members to add.");

            var nextTotalMembers = activeMemberIds.Count + addedTargetIds.Count;
            if (nextTotalMembers > MaxGroupConversationMembers)
                throw new BadRequestException($"A group can contain at most {MaxGroupConversationMembers} members.");

            var addedTargets = orderedTargets
                .Where(target => addedTargetIds.Contains(target.AccountId))
                .ToList();

            var systemMessage = new Message
            {
                ConversationId = conversationId,
                AccountId = currentId,
                MessageType = MessageTypeEnum.System,
                Content = BuildMembersAddedSystemMessageFallback(actor.Username, addedTargets.Select(target => target.Username)),
                SystemMessageDataJson = JsonSerializer.Serialize(new
                {
                    action = (int)SystemMessageActionEnum.MemberAdded,
                    actorAccountId = currentId,
                    actorUsername = actor.Username,
                    targetAccountIds = addedTargets.Select(target => target.AccountId).ToList(),
                    targetUsernames = addedTargets.Select(target => target.Username).ToList(),
                    addedCount = addedTargets.Count
                }),
                SentAt = nowUtc,
                IsEdited = false,
                IsRecalled = false
            };

            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                if (membersToInsert.Count > 0)
                {
                    await _conversationMemberRepository.AddConversationMembers(membersToInsert);
                }

                foreach (var member in membersToReactivate)
                {
                    await _conversationMemberRepository.UpdateConversationMember(member);
                }

                await _messageRepository.AddMessageAsync(systemMessage);
                return true;
            });

            var muteMap = await _conversationMemberRepository.GetMembersWithMuteStatusAsync(conversationId);
            var realtimeMessage = _mapper.Map<SendMessageResponse>(systemMessage);
            realtimeMessage.Sender = _mapper.Map<AccountChatInfoResponse>(actor);
            await _realtimeService.NotifyNewMessageAsync(conversationId, muteMap, realtimeMessage);
        }

        public async Task KickGroupMemberAsync(Guid conversationId, Guid currentId, Guid targetAccountId)
        {
            if (targetAccountId == Guid.Empty)
                throw new BadRequestException("Target account is required.");

            if (currentId == targetAccountId)
                throw new BadRequestException("You cannot kick yourself from the group.");

            var conversation = await _conversationRepository.GetConversationByIdAsync(conversationId);
            if (conversation == null)
                throw new NotFoundException($"Conversation with ID {conversationId} does not exist.");

            if (!conversation.IsGroup)
                throw new BadRequestException("Kick is only available for group conversations.");

            var actorMember = await _conversationMemberRepository.GetConversationMemberAsync(conversationId, currentId);
            if (actorMember == null)
                throw new ForbiddenException("You are not a member of this conversation.");

            var ownerId = ResolveGroupOwnerId(conversation);
            var actorIsOwner = ownerId.HasValue && ownerId.Value == currentId;
            if (!actorMember.IsAdmin && !actorIsOwner)
                throw new ForbiddenException("Only group admins can kick members.");

            var targetMember = await _conversationMemberRepository.GetConversationMemberAsync(conversationId, targetAccountId);
            if (targetMember == null)
                throw new BadRequestException("Target account is not an active member of this group.");

            if (ownerId.HasValue && ownerId.Value == targetAccountId)
                throw new BadRequestException("You cannot kick the group owner.");

            if (!actorIsOwner && targetMember.IsAdmin)
                throw new BadRequestException("Only group owner can kick another admin.");

            var accounts = await _accountRepository.GetAccountsByIds(new[] { currentId, targetAccountId });
            var actor = accounts.FirstOrDefault(a => a.AccountId == currentId);
            if (actor == null)
                throw new NotFoundException($"Account with ID {currentId} does not exist.");

            var target = accounts.FirstOrDefault(a => a.AccountId == targetAccountId);
            if (target == null)
                throw new NotFoundException($"Account with ID {targetAccountId} does not exist.");

            targetMember.IsAdmin = false;
            targetMember.HasLeft = true;
            var sentAt = DateTime.UtcNow;
            var systemMessage = new Message
            {
                ConversationId = conversationId,
                AccountId = currentId,
                MessageType = MessageTypeEnum.System,
                Content = BuildMemberKickedSystemMessageFallback(actor.Username, target.Username),
                SystemMessageDataJson = JsonSerializer.Serialize(new
                {
                    action = (int)SystemMessageActionEnum.MemberKicked,
                    actorAccountId = currentId,
                    actorUsername = actor.Username,
                    targetAccountId,
                    targetUsername = target.Username
                }),
                SentAt = sentAt,
                IsEdited = false,
                IsRecalled = false
            };

            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                await _conversationMemberRepository.UpdateConversationMember(targetMember);
                await _messageRepository.AddMessageAsync(systemMessage);
                return true;
            });

            await _realtimeService.NotifyConversationRemovedAsync(targetAccountId, conversationId, "kicked");

            var muteMap = await _conversationMemberRepository.GetMembersWithMuteStatusAsync(conversationId);
            var realtimeMessage = _mapper.Map<SendMessageResponse>(systemMessage);
            realtimeMessage.Sender = _mapper.Map<AccountChatInfoResponse>(actor);
            await _realtimeService.NotifyNewMessageAsync(conversationId, muteMap, realtimeMessage);
        }

        public async Task AssignGroupAdminAsync(Guid conversationId, Guid currentId, Guid targetAccountId)
        {
            if (targetAccountId == Guid.Empty)
                throw new BadRequestException("Target account is required.");

            var conversation = await _conversationRepository.GetConversationByIdAsync(conversationId);
            if (conversation == null)
                throw new NotFoundException($"Conversation with ID {conversationId} does not exist.");

            if (!conversation.IsGroup)
                throw new BadRequestException("Assign admin is only available for group conversations.");

            var actorMember = await _conversationMemberRepository.GetConversationMemberAsync(conversationId, currentId);
            if (actorMember == null)
                throw new ForbiddenException("You are not a member of this conversation.");

            var ownerId = ResolveGroupOwnerId(conversation);
            if (!ownerId.HasValue || ownerId.Value != currentId)
                throw new ForbiddenException("Only group owner can assign another admin.");

            var targetMember = await _conversationMemberRepository.GetConversationMemberAsync(conversationId, targetAccountId);
            if (targetMember == null)
                throw new BadRequestException("Target account is not an active member of this group.");

            if (ownerId.Value == targetAccountId)
                throw new BadRequestException("Group owner already has highest privileges.");

            if (targetMember.IsAdmin)
                throw new BadRequestException("Target member is already an admin.");

            targetMember.IsAdmin = true;

            var accounts = await _accountRepository.GetAccountsByIds(new[] { currentId, targetAccountId });
            var actor = accounts.FirstOrDefault(a => a.AccountId == currentId);
            if (actor == null)
                throw new NotFoundException($"Account with ID {currentId} does not exist.");

            var target = accounts.FirstOrDefault(a => a.AccountId == targetAccountId);
            if (target == null)
                throw new NotFoundException($"Account with ID {targetAccountId} does not exist.");

            var sentAt = DateTime.UtcNow;
            var systemMessage = new Message
            {
                ConversationId = conversationId,
                AccountId = currentId,
                MessageType = MessageTypeEnum.System,
                Content = BuildAdminGrantedSystemMessageFallback(actor.Username, target.Username),
                SystemMessageDataJson = JsonSerializer.Serialize(new
                {
                    action = (int)SystemMessageActionEnum.AdminGranted,
                    actorAccountId = currentId,
                    actorUsername = actor.Username,
                    targetAccountId,
                    targetUsername = target.Username
                }),
                SentAt = sentAt,
                IsEdited = false,
                IsRecalled = false
            };

            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                await _conversationMemberRepository.UpdateConversationMember(targetMember);
                await _messageRepository.AddMessageAsync(systemMessage);
                return true;
            });

            var muteMap = await _conversationMemberRepository.GetMembersWithMuteStatusAsync(conversationId);
            var realtimeMessage = _mapper.Map<SendMessageResponse>(systemMessage);
            realtimeMessage.Sender = _mapper.Map<AccountChatInfoResponse>(actor);
            await _realtimeService.NotifyNewMessageAsync(conversationId, muteMap, realtimeMessage);
        }

        public async Task RevokeGroupAdminAsync(Guid conversationId, Guid currentId, Guid targetAccountId)
        {
            if (targetAccountId == Guid.Empty)
                throw new BadRequestException("Target account is required.");

            if (targetAccountId == currentId)
                throw new BadRequestException("You cannot revoke your own admin role.");

            var conversation = await _conversationRepository.GetConversationByIdAsync(conversationId);
            if (conversation == null)
                throw new NotFoundException($"Conversation with ID {conversationId} does not exist.");

            if (!conversation.IsGroup)
                throw new BadRequestException("Revoke admin is only available for group conversations.");

            var actorMember = await _conversationMemberRepository.GetConversationMemberAsync(conversationId, currentId);
            if (actorMember == null)
                throw new ForbiddenException("You are not a member of this conversation.");

            var ownerId = ResolveGroupOwnerId(conversation);
            if (!ownerId.HasValue || ownerId.Value != currentId)
                throw new ForbiddenException("Only group owner can revoke admin privileges.");

            if (ownerId.Value == targetAccountId)
                throw new BadRequestException("You cannot revoke admin role from the group owner.");

            var targetMember = await _conversationMemberRepository.GetConversationMemberAsync(conversationId, targetAccountId);
            if (targetMember == null)
                throw new BadRequestException("Target account is not an active member of this group.");

            if (!targetMember.IsAdmin)
                throw new BadRequestException("Target member is not an admin.");

            targetMember.IsAdmin = false;

            var accounts = await _accountRepository.GetAccountsByIds(new[] { currentId, targetAccountId });
            var actor = accounts.FirstOrDefault(a => a.AccountId == currentId);
            if (actor == null)
                throw new NotFoundException($"Account with ID {currentId} does not exist.");

            var target = accounts.FirstOrDefault(a => a.AccountId == targetAccountId);
            if (target == null)
                throw new NotFoundException($"Account with ID {targetAccountId} does not exist.");

            var sentAt = DateTime.UtcNow;
            var systemMessage = new Message
            {
                ConversationId = conversationId,
                AccountId = currentId,
                MessageType = MessageTypeEnum.System,
                Content = BuildAdminRevokedSystemMessageFallback(actor.Username, target.Username),
                SystemMessageDataJson = JsonSerializer.Serialize(new
                {
                    action = (int)SystemMessageActionEnum.AdminRevoked,
                    actorAccountId = currentId,
                    actorUsername = actor.Username,
                    targetAccountId,
                    targetUsername = target.Username
                }),
                SentAt = sentAt,
                IsEdited = false,
                IsRecalled = false
            };

            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                await _conversationMemberRepository.UpdateConversationMember(targetMember);
                await _messageRepository.AddMessageAsync(systemMessage);
                return true;
            });

            var muteMap = await _conversationMemberRepository.GetMembersWithMuteStatusAsync(conversationId);
            var realtimeMessage = _mapper.Map<SendMessageResponse>(systemMessage);
            realtimeMessage.Sender = _mapper.Map<AccountChatInfoResponse>(actor);
            await _realtimeService.NotifyNewMessageAsync(conversationId, muteMap, realtimeMessage);
        }

        public async Task TransferGroupOwnerAsync(Guid conversationId, Guid currentId, Guid targetAccountId)
        {
            if (targetAccountId == Guid.Empty)
                throw new BadRequestException("Target account is required.");

            if (targetAccountId == currentId)
                throw new BadRequestException("Please choose another member as the new owner.");

            var conversation = await _conversationRepository.GetConversationByIdAsync(conversationId);
            if (conversation == null)
                throw new NotFoundException($"Conversation with ID {conversationId} does not exist.");

            if (!conversation.IsGroup)
                throw new BadRequestException("Ownership transfer is only available for group conversations.");

            var actorMember = await _conversationMemberRepository.GetConversationMemberAsync(conversationId, currentId);
            if (actorMember == null)
                throw new ForbiddenException("You are not a member of this conversation.");

            var ownerId = ResolveGroupOwnerId(conversation);
            if (!ownerId.HasValue || ownerId.Value != currentId)
                throw new ForbiddenException("Only group owner can transfer ownership.");

            var targetMember = await _conversationMemberRepository.GetConversationMemberAsync(conversationId, targetAccountId);
            if (targetMember == null)
                throw new BadRequestException("Target account is not an active member of this group.");

            conversation.Owner = targetAccountId;
            targetMember.IsAdmin = true;

            var accounts = await _accountRepository.GetAccountsByIds(new[] { currentId, targetAccountId });
            var actor = accounts.FirstOrDefault(a => a.AccountId == currentId);
            if (actor == null)
                throw new NotFoundException($"Account with ID {currentId} does not exist.");

            var target = accounts.FirstOrDefault(a => a.AccountId == targetAccountId);
            if (target == null)
                throw new NotFoundException($"Account with ID {targetAccountId} does not exist.");

            var sentAt = DateTime.UtcNow;
            var systemMessage = new Message
            {
                ConversationId = conversationId,
                AccountId = currentId,
                MessageType = MessageTypeEnum.System,
                Content = BuildOwnerTransferredSystemMessageFallback(actor.Username, target.Username),
                SystemMessageDataJson = JsonSerializer.Serialize(new
                {
                    action = (int)SystemMessageActionEnum.OwnerTransferred,
                    actorAccountId = currentId,
                    actorUsername = actor.Username,
                    targetAccountId,
                    targetUsername = target.Username,
                    ownerAccountId = targetAccountId
                }),
                SentAt = sentAt,
                IsEdited = false,
                IsRecalled = false
            };

            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                await _conversationRepository.UpdateConversationAsync(conversation);
                await _conversationMemberRepository.UpdateConversationMember(targetMember);
                await _messageRepository.AddMessageAsync(systemMessage);
                return true;
            });

            var muteMap = await _conversationMemberRepository.GetMembersWithMuteStatusAsync(conversationId);
            var realtimeMessage = _mapper.Map<SendMessageResponse>(systemMessage);
            realtimeMessage.Sender = _mapper.Map<AccountChatInfoResponse>(actor);
            await _realtimeService.NotifyNewMessageAsync(conversationId, muteMap, realtimeMessage);

            await _realtimeService.NotifyGroupConversationInfoUpdatedAsync(
                conversationId,
                conversation.ConversationName,
                conversation.ConversationAvatar,
                conversation.Owner,
                currentId);
        }

        public async Task LeaveGroupAsync(Guid conversationId, Guid currentId)
        {
            var conversation = await _conversationRepository.GetConversationByIdAsync(conversationId);
            if (conversation == null)
                throw new NotFoundException($"Conversation with ID {conversationId} does not exist.");

            if (!conversation.IsGroup)
                throw new BadRequestException("Leave is only available for group conversations.");

            var actorMember = await _conversationMemberRepository.GetConversationMemberAsync(conversationId, currentId);
            if (actorMember == null)
                throw new ForbiddenException("You are not a member of this conversation.");

            var ownerId = ResolveGroupOwnerId(conversation);
            if (ownerId.HasValue && ownerId.Value == currentId)
                throw new BadRequestException("Group owner must transfer ownership before leaving the group.");

            var activeMembers = await _conversationMemberRepository.GetConversationMembersAsync(conversationId);
            if (activeMembers.Count == 0)
                throw new BadRequestException("This group has no active members.");

            var actor = activeMembers
                .FirstOrDefault(member => member.AccountId == currentId)
                ?.Account;
            if (actor == null)
                throw new NotFoundException($"Account with ID {currentId} does not exist.");

            actorMember.IsAdmin = false;
            actorMember.HasLeft = true;
            var sentAt = DateTime.UtcNow;
            var systemMessage = new Message
            {
                ConversationId = conversationId,
                AccountId = currentId,
                MessageType = MessageTypeEnum.System,
                Content = BuildMemberLeftSystemMessageFallback(actor.Username),
                SystemMessageDataJson = JsonSerializer.Serialize(new
                {
                    action = (int)SystemMessageActionEnum.MemberLeft,
                    actorAccountId = currentId,
                    actorUsername = actor.Username
                }),
                SentAt = sentAt,
                IsEdited = false,
                IsRecalled = false
            };

            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                await _conversationMemberRepository.UpdateConversationMember(actorMember);
                await _messageRepository.AddMessageAsync(systemMessage);
                return true;
            });

            await _realtimeService.NotifyConversationRemovedAsync(currentId, conversationId, "left");

            var muteMap = await _conversationMemberRepository.GetMembersWithMuteStatusAsync(conversationId);
            var realtimeMessage = _mapper.Map<SendMessageResponse>(systemMessage);
            realtimeMessage.Sender = _mapper.Map<AccountChatInfoResponse>(actor);
            await _realtimeService.NotifyNewMessageAsync(conversationId, muteMap, realtimeMessage);
        }

        public async Task<bool> IsMemberAsync(Guid conversationId, Guid accountId)
        {
            return await _conversationMemberRepository.IsMemberOfConversation(conversationId, accountId);
        }
        public async Task SoftDeleteChatHistory(Guid conversationId, Guid currentId)
        {
            var member = await _conversationMemberRepository.GetConversationMemberAsync(conversationId, currentId);
            if (member == null)
                throw new ForbiddenException($"Account with ID {currentId} is not a member of this conversation.");
            member.ClearedAt = DateTime.UtcNow;
            await _conversationMemberRepository.UpdateConversationMember(member);
            await _unitOfWork.CommitAsync();
            await _realtimeService.NotifyConversationRemovedAsync(currentId, conversationId, "soft-delete");
        }
        public async Task MarkSeenAsync(Guid conversationId, Guid currentId, Guid newMessageId)
        {
            var member = await _conversationMemberRepository.GetConversationMemberAsync(conversationId, currentId);
            if (member == null)
                throw new ForbiddenException($"Account with ID {currentId} is not a member of this conversation.");
            if(member.LastSeenMessageId == null || await _messageRepository.IsMessageNewer(newMessageId, member.LastSeenMessageId.Value))
            {
                member.LastSeenMessageId = newMessageId;
                member.LastSeenAt = DateTime.UtcNow;
                await _conversationMemberRepository.UpdateConversationMember(member);
                await _unitOfWork.CommitAsync();
            }        
        }

        private static string BuildNicknameSystemMessageFallback(string actorUsername, string targetUsername, string? nickname)
        {
            var actorMention = ToMention(actorUsername);
            var targetMention = ToMention(targetUsername);

            if (string.IsNullOrWhiteSpace(nickname))
            {
                return $"{actorMention} removed nickname for {targetMention}.";
            }

            return $"{actorMention} set nickname for {targetMention} to \"{nickname}\".";
        }

        private static string ToMention(string? username)
        {
            var normalized = (username ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return "@unknown";
            }

            return normalized.StartsWith("@", StringComparison.Ordinal)
                ? normalized
                : $"@{normalized}";
        }

        private static string BuildThemeSystemMessageFallback(string actorUsername, string? theme)
        {
            var actorMention = ToMention(actorUsername);
            if (string.IsNullOrWhiteSpace(theme))
            {
                return $"{actorMention} reset the chat theme.";
            }

            return $"{actorMention} changed the chat theme to \"{theme}\".";
        }

        private static string BuildMemberKickedSystemMessageFallback(string actorUsername, string targetUsername)
        {
            var actorMention = ToMention(actorUsername);
            var targetMention = ToMention(targetUsername);
            return $"{actorMention} removed {targetMention} from the group.";
        }

        private static string BuildMemberLeftSystemMessageFallback(string actorUsername)
        {
            var actorMention = ToMention(actorUsername);
            return $"{actorMention} left the group.";
        }

        private static string BuildAdminGrantedSystemMessageFallback(string actorUsername, string targetUsername)
        {
            var actorMention = ToMention(actorUsername);
            var targetMention = ToMention(targetUsername);
            return $"{actorMention} made {targetMention} an admin.";
        }

        private static string BuildAdminRevokedSystemMessageFallback(string actorUsername, string targetUsername)
        {
            var actorMention = ToMention(actorUsername);
            var targetMention = ToMention(targetUsername);
            return $"{actorMention} removed admin role from {targetMention}.";
        }

        private static string BuildOwnerTransferredSystemMessageFallback(string actorUsername, string targetUsername)
        {
            var actorMention = ToMention(actorUsername);
            var targetMention = ToMention(targetUsername);
            return $"{actorMention} transferred group ownership to {targetMention}.";
        }

        private static string BuildMembersAddedSystemMessageFallback(string actorUsername, IEnumerable<string> targetUsernames)
        {
            var actorMention = ToMention(actorUsername);
            var targetMentions = (targetUsernames ?? Enumerable.Empty<string>())
                .Select(ToMention)
                .Where(mention => !string.IsNullOrWhiteSpace(mention))
                .Distinct()
                .ToList();

            if (targetMentions.Count == 0)
            {
                return $"{actorMention} added new members to the group.";
            }

            return $"{actorMention} added {string.Join(", ", targetMentions)} to the group.";
        }

        private static string? NormalizeTheme(string? theme)
        {
            if (string.IsNullOrWhiteSpace(theme))
                return null;

            var normalized = theme.Trim().ToLowerInvariant();
            return normalized.Length > MaxConversationThemeLength
                ? normalized[..MaxConversationThemeLength]
                : normalized;
        }

        private static Guid? ResolveGroupOwnerId(Conversation conversation)
        {
            if (conversation == null || !conversation.IsGroup)
                return null;

            if (conversation.Owner.HasValue && conversation.Owner.Value != Guid.Empty)
                return conversation.Owner.Value;

            return conversation.CreatedBy != Guid.Empty ? conversation.CreatedBy : null;
        }
    }
}
