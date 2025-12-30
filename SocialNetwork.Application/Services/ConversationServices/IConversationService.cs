using SocialNetwork.Application.DTOs.ConversationDTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Application.Services.ConversationServices
{
    public interface IConversationService
    {
        Task<ConversationResponse?> GetPrivateConversationAsync(Guid currentId, Guid otherId);
        Task<ConversationResponse> CreatePrivateConversationAsync(Guid currentId, Guid otherId);
    }
}
