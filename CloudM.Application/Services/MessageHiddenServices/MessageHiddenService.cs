using CloudM.Domain.Entities;
using CloudM.Infrastructure.Repositories.MessageHiddens;
using CloudM.Infrastructure.Repositories.UnitOfWork;
using CloudM.Infrastructure.Repositories.Messages;
using CloudM.Application.Services.RealtimeServices;
using System;
using System.Threading.Tasks;

namespace CloudM.Application.Services.MessageHiddenServices
{
    public class MessageHiddenService : IMessageHiddenService
    {
        private readonly IMessageHiddenRepository _messageHiddenRepository;
        private readonly IMessageRepository _messageRepository;
        private readonly IRealtimeService _realtimeService;
        private readonly IUnitOfWork _unitOfWork;

        public MessageHiddenService(IMessageHiddenRepository messageHiddenRepository, 
            IMessageRepository messageRepository,
            IRealtimeService realtimeService,
            IUnitOfWork unitOfWork)
        {
            _messageHiddenRepository = messageHiddenRepository;
            _messageRepository = messageRepository;
            _realtimeService = realtimeService;
            _unitOfWork = unitOfWork;
        }

        public async Task HideMessageAsync(Guid messageId, Guid accountId)
        {
            if (await _messageHiddenRepository.IsMessageHiddenByAccountAsync(messageId, accountId))
                return;

            var message = await _messageRepository.GetMessageByIdAsync(messageId);
            if (message == null) return;

            await _messageHiddenRepository.HideMessageAsync(new MessageHidden
            {
                MessageId = messageId,
                AccountId = accountId,
                HiddenAt = DateTime.UtcNow
            });

            await _unitOfWork.CommitAsync();

            // Notify current user's other devices
            await _realtimeService.NotifyMessageHiddenAsync(accountId, message.ConversationId, messageId);
        }
    }
}
