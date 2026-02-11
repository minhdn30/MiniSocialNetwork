using System;
using System.Threading.Tasks;

namespace SocialNetwork.Application.Services.MessageHiddenServices
{
    public interface IMessageHiddenService
    {
        Task HideMessageAsync(Guid messageId, Guid accountId);
    }
}
