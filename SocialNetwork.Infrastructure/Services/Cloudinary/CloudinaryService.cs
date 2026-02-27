using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using SocialNetwork.Domain.Enums;
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SocialNetwork.Infrastructure.Services.Cloudinary
{
    public class CloudinaryService : ICloudinaryService
    {
        private readonly CloudinaryDotNet.Cloudinary _cloudinary;
        private readonly ICloudinaryDeleteBackgroundQueue _deleteQueue;

        public CloudinaryService(
            IConfiguration config,
            ICloudinaryDeleteBackgroundQueue deleteQueue)
        {
            var account = new CloudinaryDotNet.Account(
                config["Cloudinary:CloudName"],
                config["Cloudinary:ApiKey"],
                config["Cloudinary:ApiSecret"]
            );

            _cloudinary = new CloudinaryDotNet.Cloudinary(account);
            _deleteQueue = deleteQueue;
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

        public async Task<string?> UploadRawFileAsync(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return null;

            await using var stream = file.OpenReadStream();

            var uploadParams = new RawUploadParams
            {
                File = new FileDescription(file.FileName, stream),
                Folder = "cloudmsocialnetwork/files",
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
                var isRawResource = segments.Any(s => s.Equals("raw", StringComparison.OrdinalIgnoreCase));
                var publicId = isRawResource
                    ? pathAfterUpload
                    : Path.ChangeExtension(pathAfterUpload, null);
                //decode
                return Uri.UnescapeDataString(publicId);
            }
            catch
            {
                return null;
            }
        }

        public string? GetDownloadUrl(string mediaUrl, MediaTypeEnum type, string? fileName = null)
        {
            var publicId = GetPublicIdFromUrl(mediaUrl);
            if (string.IsNullOrWhiteSpace(publicId))
                return null;

            var resourceType = type switch
            {
                MediaTypeEnum.Video => "video",
                MediaTypeEnum.Document => "raw",
                _ => "image"
            };

            var expiresAt = DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds();

            return _cloudinary.DownloadPrivate(
                publicId: publicId,
                attachment: true,
                format: null,
                type: "upload",
                expiresAt: expiresAt,
                resourceType: resourceType);
        }


        public async Task<bool> DeleteMediaAsync(string publicId, MediaTypeEnum type)
        {
            var normalizedPublicId = (publicId ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalizedPublicId))
            {
                return false;
            }

            if (_deleteQueue.Enqueue(normalizedPublicId, type))
            {
                return true;
            }

            // Fallback: queue unavailable/full => execute immediately.
            try
            {
                var deletionParams = BuildDeletionParams(normalizedPublicId, type);
                var result = await _cloudinary.DestroyAsync(deletionParams);
                return result.Result == "ok" || result.Result == "not found";
            }
            catch
            {
                return false;
            }
        }

        internal static DeletionParams BuildDeletionParams(string publicId, MediaTypeEnum type)
        {
            var deletionParams = new DeletionParams(publicId);

            if (type == MediaTypeEnum.Video)
            {
                deletionParams.ResourceType = ResourceType.Video;
            }
            else if (type == MediaTypeEnum.Document)
            {
                deletionParams.ResourceType = ResourceType.Raw;
            }

            return deletionParams;
        }

    }
}
