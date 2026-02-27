using SocialNetwork.Domain.Enums;

namespace SocialNetwork.Infrastructure.Services.Cloudinary
{
    public readonly record struct CloudinaryDeleteRequest(string PublicId, MediaTypeEnum Type);
}
