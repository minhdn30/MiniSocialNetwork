using SocialNetwork.Application.DTOs.MessageDTOs;
using SocialNetwork.Domain.Enums;
using System;
using System.Threading.Tasks;

namespace SocialNetwork.Application.Services.MessageReactServices
{
    public interface IMessageReactService
    {
        Task<MessageReactStateResponse> GetMessageReactStateAsync(Guid messageId, Guid accountId);
        Task<MessageReactStateResponse> SetMessageReactAsync(Guid messageId, Guid accountId, ReactEnum reactType);
        Task<MessageReactStateResponse> RemoveMessageReactAsync(Guid messageId, Guid accountId);
    }
}
