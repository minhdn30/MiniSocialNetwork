using Microsoft.AspNetCore.Http;
using SocialNetwork.Domain.Enums;
using System.Threading.Tasks;

namespace SocialNetwork.Infrastructure.Services.Cloudinary
{
    public interface ICloudinaryService
    {
        Task<string?> UploadImageAsync(IFormFile file);
        Task<string?> UploadVideoAsync(IFormFile file);
        string? GetPublicIdFromUrl(string imageUrl);
        Task<bool> DeleteMediaAsync(string publicId, MediaTypeEnum type);
    }
}
