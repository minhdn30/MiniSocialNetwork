using CloudM.Domain.Entities;
using CloudM.Infrastructure.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CloudM.Infrastructure.Repositories.Messages
{
    public interface IMessageRepository
    {
        Task<(IReadOnlyList<MessageBasicModel> Items, string? OlderCursor, string? NewerCursor, bool HasMoreOlder, bool HasMoreNewer)>
            GetMessagesByConversationId(Guid conversationId, Guid currentId, string? cursor, int pageSize);
        Task AddMessageAsync(Message message);
        Task<bool> IsMessageNewer(Guid newMessageId, Guid? lastSeenMessageId);
        Task<int> CountUnreadMessagesAsync(Guid conversationId, Guid currentId, DateTime? lastSeenAt);
        Task<Message?> GetMessageByIdAsync(Guid messageId);
        Task<int> GetMessagePositionAsync(Guid conversationId, Guid currentId, Guid messageId);
        Task<(IEnumerable<MessageBasicModel> items, int totalItems)> SearchMessagesAsync(Guid conversationId, Guid currentId, string keyword, int page, int pageSize);
        Task<(IEnumerable<ConversationMediaItemModel> items, int totalItems)> GetConversationMediaAsync(Guid conversationId, Guid currentId, int page, int pageSize);
        Task<(IEnumerable<ConversationMediaItemModel> items, int totalItems)> GetConversationFilesAsync(Guid conversationId, Guid currentId, int page, int pageSize);
    }
}
