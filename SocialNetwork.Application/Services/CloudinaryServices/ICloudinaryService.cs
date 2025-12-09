using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Application.Services.CloudinaryServices
{
    public interface ICloudinaryService
    {
        Task<string?> UploadImageAsync(IFormFile file);
        Task<string?> UploadVideoAsync(IFormFile file);
        string? GetPublicIdFromUrl(string imageUrl);
        Task<bool> DeleteMediaAsync(string publicId);
    }
}
