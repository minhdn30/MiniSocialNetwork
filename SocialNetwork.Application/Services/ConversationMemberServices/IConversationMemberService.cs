using SocialNetwork.Application.DTOs.ConversationMemberDTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Application.Services.ConversationMemberServices
{
    public interface IConversationMemberService
    {
        Task<bool> IsMemberAsync(Guid conversationId, Guid accountId);
        Task SetMuteStatusAsync(Guid conversationId, Guid currentId, bool isMuted);
        Task SetThemeAsync(Guid conversationId, Guid currentId, ConversationThemeUpdateRequest request);
        Task UpdateMemberNickname(Guid conversationId, Guid currentId, ConversationMemberNicknameUpdateRequest request);
        Task SoftDeleteChatHistory(Guid conversationId, Guid currentId);
        Task MarkSeenAsync(Guid conversationId, Guid currentId, Guid newMessageId);
    }
}
