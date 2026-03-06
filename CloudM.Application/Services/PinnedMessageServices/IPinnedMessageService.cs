using CloudM.Application.DTOs.CommonDTOs;
using CloudM.Application.DTOs.PinnedMessageDTOs;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CloudM.Application.Services.PinnedMessageServices
{
    public interface IPinnedMessageService
    {
        Task<IEnumerable<PinnedMessageResponse>> GetPinnedMessagesAsync(Guid conversationId, Guid currentAccountId);
        Task<PagedResponse<PinnedMessageResponse>> GetPinnedMessagesAsync(Guid conversationId, Guid currentAccountId, int page, int pageSize);
        Task PinMessageAsync(Guid conversationId, Guid messageId, Guid currentAccountId);
        Task UnpinMessageAsync(Guid conversationId, Guid messageId, Guid currentAccountId);
    }
}
