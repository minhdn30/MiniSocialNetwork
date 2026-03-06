using CloudM.Application.DTOs.ConversationDTOs;
using CloudM.Application.DTOs.ConversationMemberDTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CloudM.Application.Services.ConversationMemberServices
{
    public interface IConversationMemberService
    {
        Task<bool> IsMemberAsync(Guid conversationId, Guid accountId);
        Task SetMuteStatusAsync(Guid conversationId, Guid currentId, bool isMuted);
        Task SetThemeAsync(Guid conversationId, Guid currentId, ConversationThemeUpdateRequest request);
        Task UpdateMemberNickname(Guid conversationId, Guid currentId, ConversationMemberNicknameUpdateRequest request);
        Task<List<GroupInviteAccountSearchResponse>> SearchAccountsForGroupInviteAsync(Guid currentId, string keyword, IEnumerable<Guid>? excludeAccountIds, int limit = 10);
        Task<List<GroupInviteAccountSearchResponse>> SearchAccountsForAddGroupMembersAsync(Guid conversationId, Guid currentId, string keyword, IEnumerable<Guid>? excludeAccountIds, int limit = 10);
        Task AddGroupMembersAsync(Guid conversationId, Guid currentId, AddGroupMembersRequest request);
        Task KickGroupMemberAsync(Guid conversationId, Guid currentId, Guid targetAccountId);
        Task AssignGroupAdminAsync(Guid conversationId, Guid currentId, Guid targetAccountId);
        Task RevokeGroupAdminAsync(Guid conversationId, Guid currentId, Guid targetAccountId);
        Task TransferGroupOwnerAsync(Guid conversationId, Guid currentId, Guid targetAccountId);
        Task LeaveGroupAsync(Guid conversationId, Guid currentId);
        Task SoftDeleteChatHistory(Guid conversationId, Guid currentId);
        Task MarkSeenAsync(Guid conversationId, Guid currentId, Guid newMessageId);
    }
}
