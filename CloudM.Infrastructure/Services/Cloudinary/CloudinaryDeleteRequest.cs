using CloudM.Domain.Enums;

namespace CloudM.Infrastructure.Services.Cloudinary
{
    public readonly record struct CloudinaryDeleteRequest(string PublicId, MediaTypeEnum Type);
}
