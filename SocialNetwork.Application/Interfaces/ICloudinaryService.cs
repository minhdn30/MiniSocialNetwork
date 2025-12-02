using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Application.Interfaces
{
    public interface ICloudinaryService
    {
        Task<string?> UploadImageAsync(IFormFile file);
        string? GetPublicIdFromUrl(string imageUrl);
        Task<bool> DeleteImageAsync(string publicId);
    }
}
