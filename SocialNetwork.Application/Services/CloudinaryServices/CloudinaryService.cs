using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using SocialNetwork.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Application.Services.CloudinaryServices
{
    public class CloudinaryService : ICloudinaryService
    {
        private readonly Cloudinary _cloudinary;
        public CloudinaryService(IConfiguration config)
        {
            var account = new CloudinaryDotNet.Account(
                config["Cloudinary:CloudName"],
                config["Cloudinary:ApiKey"],
                config["Cloudinary:ApiSecret"]
            );

            _cloudinary = new Cloudinary(account);
        }
        public async Task<string?> UploadImageAsync(IFormFile file)
        {
            if (file == null || file.Length == 0) return null;

            await using var stream = file.OpenReadStream();
            var uploadParams = new ImageUploadParams
            {
                File = new FileDescription(file.FileName, stream),
                Folder = "cloudmsocialnetwork/images",
                UseFilename = true,
                UniqueFilename = true,
                Overwrite = false
            };

            var result = await _cloudinary.UploadAsync(uploadParams);
            return result.SecureUrl?.ToString();
        }
        public async Task<string?> UploadVideoAsync(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return null;

            await using var stream = file.OpenReadStream();

            var uploadParams = new VideoUploadParams
            {
                File = new FileDescription(file.FileName, stream),
                Folder = "cloudmsocialnetwork/videos", 
                UseFilename = true,
                UniqueFilename = true,
                Overwrite = false
            };

            var result = await _cloudinary.UploadAsync(uploadParams);

            return result.SecureUrl?.ToString();
        }

        public string? GetPublicIdFromUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return null;

            try
            {
                var uri = new Uri(url);

                var path = uri.AbsolutePath.TrimStart('/');
                var lastDotIndex = path.LastIndexOf('.');
                if (lastDotIndex > 0)
                    path = path[..lastDotIndex];
                return path;
            }
            catch
            {
                return null;
            }
        }



        public async Task<bool> DeleteMediaAsync(string publicId)
        {
            var deletionParams = new DeletionParams(publicId);

            var result = await _cloudinary.DestroyAsync(deletionParams);

            return result.Result == "ok" || result.Result == "not found";
        }
    }
}
