using System.Threading;
using System.Threading.Tasks;
using CloudM.Domain.Enums;

namespace CloudM.Infrastructure.Services.Cloudinary
{
    public interface ICloudinaryDeleteBackgroundQueue
    {
        bool Enqueue(string publicId, MediaTypeEnum type);
        ValueTask<CloudinaryDeleteRequest> DequeueAsync(CancellationToken cancellationToken);
    }
}
