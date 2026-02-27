using System;
using System.Threading.Tasks;

namespace CloudM.Application.Services.MessageHiddenServices
{
    public interface IMessageHiddenService
    {
        Task HideMessageAsync(Guid messageId, Guid accountId);
    }
}
