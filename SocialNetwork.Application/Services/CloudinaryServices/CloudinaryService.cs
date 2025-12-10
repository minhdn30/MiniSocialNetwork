using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using SocialNetwork.Domain.Entities;
using SocialNetwork.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
                var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

                var uploadIndex = Array.FindIndex(segments, s => s.Equals("upload", StringComparison.OrdinalIgnoreCase));
                if (uploadIndex < 0 || uploadIndex == segments.Length - 1)
                    return null;

                var pathAfterUpload = string.Join("/", segments.Skip(uploadIndex + 1));

                pathAfterUpload = Regex.Replace(pathAfterUpload, @"^v\d+/", "");
                var publicId = Path.ChangeExtension(pathAfterUpload, null);
                //decode
                return Uri.UnescapeDataString(publicId);
            }
            catch
            {
                return null;
            }
        }


        public async Task<bool> DeleteMediaAsync(string publicId, MediaTypeEnum type)
        {
            var deletionParams = new DeletionParams(publicId);

            //Specify resource type if it is video
            if (type == MediaTypeEnum.Video)
            {
                deletionParams.ResourceType = ResourceType.Video;
            }

            var result = await _cloudinary.DestroyAsync(deletionParams);

            return result.Result == "ok" || result.Result == "not found";
        }

    }
}
