using System.Threading;
using System.Threading.Tasks;
using SocialNetwork.Domain.Enums;

namespace SocialNetwork.Infrastructure.Services.Cloudinary
{
    public interface ICloudinaryDeleteBackgroundQueue
    {
        bool Enqueue(string publicId, MediaTypeEnum type);
        ValueTask<CloudinaryDeleteRequest> DequeueAsync(CancellationToken cancellationToken);
    }
}
