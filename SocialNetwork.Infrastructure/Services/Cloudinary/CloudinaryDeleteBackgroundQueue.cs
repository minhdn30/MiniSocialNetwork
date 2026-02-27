using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using SocialNetwork.Domain.Enums;

namespace SocialNetwork.Infrastructure.Services.Cloudinary
{
    public class CloudinaryDeleteBackgroundQueue : ICloudinaryDeleteBackgroundQueue
    {
        private const int DefaultQueueCapacity = 5000;
        private readonly Channel<CloudinaryDeleteRequest> _channel;

        public CloudinaryDeleteBackgroundQueue()
        {
            _channel = Channel.CreateBounded<CloudinaryDeleteRequest>(new BoundedChannelOptions(DefaultQueueCapacity)
            {
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false,
                FullMode = BoundedChannelFullMode.DropWrite
            });
        }

        public bool Enqueue(string publicId, MediaTypeEnum type)
        {
            var normalizedPublicId = (publicId ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalizedPublicId))
            {
                return false;
            }

            return _channel.Writer.TryWrite(new CloudinaryDeleteRequest(normalizedPublicId, type));
        }

        public ValueTask<CloudinaryDeleteRequest> DequeueAsync(CancellationToken cancellationToken)
        {
            return _channel.Reader.ReadAsync(cancellationToken);
        }
    }
}
