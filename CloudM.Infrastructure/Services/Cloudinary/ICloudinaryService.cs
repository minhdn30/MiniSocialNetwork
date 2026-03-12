using Microsoft.AspNetCore.Http;
using CloudM.Domain.Enums;
using System.Threading.Tasks;

namespace CloudM.Infrastructure.Services.Cloudinary
{
    public interface ICloudinaryService
    {
        Task<string?> UploadImageAsync(IFormFile file);
        Task<string?> UploadVideoAsync(IFormFile file);
        Task<string?> UploadRawFileAsync(IFormFile file);
        string? GetPublicIdFromUrl(string imageUrl);
        string? GetDownloadUrl(string mediaUrl, MediaTypeEnum type, string? fileName = null);
        bool TryQueueDeleteMedia(string publicId, MediaTypeEnum type);
        bool TryQueueDeleteMediaByUrl(string? mediaUrl, MediaTypeEnum type);
        Task<bool> DeleteMediaAsync(string publicId, MediaTypeEnum type);
    }
}
