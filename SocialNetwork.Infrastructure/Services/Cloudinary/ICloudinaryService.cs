using Microsoft.AspNetCore.Http;
using SocialNetwork.Domain.Enums;
using System.Threading.Tasks;

namespace SocialNetwork.Infrastructure.Services.Cloudinary
{
    public interface ICloudinaryService
    {
        Task<string?> UploadImageAsync(IFormFile file);
        Task<string?> UploadVideoAsync(IFormFile file);
        Task<string?> UploadRawFileAsync(IFormFile file);
        string? GetPublicIdFromUrl(string imageUrl);
        string? GetDownloadUrl(string mediaUrl, MediaTypeEnum type, string? fileName = null);
        Task<bool> DeleteMediaAsync(string publicId, MediaTypeEnum type);
    }
}
