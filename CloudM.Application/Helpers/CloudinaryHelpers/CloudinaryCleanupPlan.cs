using CloudM.Domain.Enums;
using CloudM.Infrastructure.Services.Cloudinary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CloudM.Application.Helpers.CloudinaryHelpers
{
    public class CloudinaryCleanupPlan
    {
        private readonly ICloudinaryService _cloudinaryService;
        private readonly ConcurrentDictionary<CloudinaryCleanupTarget, byte> _rollbackTargets = new();
        private readonly ConcurrentDictionary<CloudinaryCleanupTarget, byte> _postCommitTargets = new();

        public CloudinaryCleanupPlan(ICloudinaryService cloudinaryService)
        {
            _cloudinaryService = cloudinaryService;
        }

        public void AddRollbackDeleteByUrl(string? mediaUrl, MediaTypeEnum type)
        {
            var publicId = _cloudinaryService.GetPublicIdFromUrl(mediaUrl ?? string.Empty);
            AddTarget(_rollbackTargets, publicId, type);
        }

        public void AddRollbackDeleteByPublicId(string? publicId, MediaTypeEnum type)
        {
            AddTarget(_rollbackTargets, publicId, type);
        }

        public void AddPostCommitDeleteByUrl(string? mediaUrl, MediaTypeEnum type)
        {
            var publicId = _cloudinaryService.GetPublicIdFromUrl(mediaUrl ?? string.Empty);
            AddTarget(_postCommitTargets, publicId, type);
        }

        public void AddPostCommitDeleteByPublicId(string? publicId, MediaTypeEnum type)
        {
            AddTarget(_postCommitTargets, publicId, type);
        }

        public async Task ExecuteRollbackAsync()
        {
            if (_rollbackTargets.Count == 0)
            {
                return;
            }

            var cleanupTasks = _rollbackTargets
                .Keys
                .Select(target => _cloudinaryService.DeleteMediaAsync(target.PublicId, target.Type))
                .ToList();

            await Task.WhenAll(cleanupTasks);
        }

        public Task ExecutePostCommitAsync()
        {
            if (_postCommitTargets.Count == 0)
            {
                return Task.CompletedTask;
            }

            foreach (var target in _postCommitTargets.Keys)
            {
                _cloudinaryService.TryQueueDeleteMedia(target.PublicId, target.Type);
            }

            return Task.CompletedTask;
        }

        private static void AddTarget(ConcurrentDictionary<CloudinaryCleanupTarget, byte> targets, string? publicId, MediaTypeEnum type)
        {
            var normalizedPublicId = (publicId ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalizedPublicId))
            {
                return;
            }

            targets.TryAdd(new CloudinaryCleanupTarget(normalizedPublicId, type), 0);
        }

        private readonly record struct CloudinaryCleanupTarget(string PublicId, MediaTypeEnum Type);
    }
}
