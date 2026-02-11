using SocialNetwork.Domain.Entities;
using SocialNetwork.Infrastructure.Repositories.MessageHiddens;
using SocialNetwork.Infrastructure.Repositories.UnitOfWork;
using System;
using System.Threading.Tasks;

namespace SocialNetwork.Application.Services.MessageHiddenServices
{
    public class MessageHiddenService : IMessageHiddenService
    {
        private readonly IMessageHiddenRepository _messageHiddenRepository;
        private readonly IUnitOfWork _unitOfWork;

        public MessageHiddenService(IMessageHiddenRepository messageHiddenRepository, IUnitOfWork unitOfWork)
        {
            _messageHiddenRepository = messageHiddenRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task HideMessageAsync(Guid messageId, Guid accountId)
        {
            if (await _messageHiddenRepository.IsMessageHiddenByAccountAsync(messageId, accountId))
                return;

            await _messageHiddenRepository.HideMessageAsync(new MessageHidden
            {
                MessageId = messageId,
                AccountId = accountId,
                HiddenAt = DateTime.UtcNow
            });

            await _unitOfWork.CommitAsync();
        }
    }
}
