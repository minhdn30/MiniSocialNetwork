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
                Folder = "cloudmsocialnetwork/services",
                UseFilename = true,
                UniqueFilename = true,
                Overwrite = false
            };

            var result = await _cloudinary.UploadAsync(uploadParams);
            return result.SecureUrl?.ToString();
        }
        public string? GetPublicIdFromUrl(string imageUrl)
        {
            if (string.IsNullOrWhiteSpace(imageUrl))
                return null;

            if (!Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri))
                return null;

            try
            {
                var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (segments.Length < 3) return null;

                var fileName = Path.GetFileNameWithoutExtension(uri.AbsolutePath);
                var folderPath = string.Join('/', segments.Skip(segments.Length - 3).Take(2)); // cloudmsocialnetwork/services

                return $"{folderPath}/{fileName}";
            }
            catch
            {
                return null;
            }
        }


        public async Task<bool> DeleteImageAsync(string publicId)
        {
            var deletionParams = new DeletionParams(publicId);

            var result = await _cloudinary.DestroyAsync(deletionParams);

            return result.Result == "ok" || result.Result == "not found";
        }
    }
}
