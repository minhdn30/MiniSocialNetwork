using CloudM.Application.DTOs.PinnedMessageDTOs;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CloudM.Application.Services.PinnedMessageServices
{
    public interface IPinnedMessageService
    {
        Task<IEnumerable<PinnedMessageResponse>> GetPinnedMessagesAsync(Guid conversationId, Guid currentAccountId);
        Task PinMessageAsync(Guid conversationId, Guid messageId, Guid currentAccountId);
        Task UnpinMessageAsync(Guid conversationId, Guid messageId, Guid currentAccountId);
    }
}
