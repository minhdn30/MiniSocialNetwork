using CloudinaryDotNet;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CloudM.Infrastructure.Services.Cloudinary
{
    public class CloudinaryDeleteWorkerHostedService : BackgroundService
    {
        private const int MaxDeleteAttempts = 3;

        private readonly ICloudinaryDeleteBackgroundQueue _deleteQueue;
        private readonly IConfiguration _configuration;
        private readonly ILogger<CloudinaryDeleteWorkerHostedService> _logger;
        private CloudinaryDotNet.Cloudinary? _cloudinary;
        private bool _isCloudinaryConfigMissingLogged;

        public CloudinaryDeleteWorkerHostedService(
            ICloudinaryDeleteBackgroundQueue deleteQueue,
            IConfiguration configuration,
            ILogger<CloudinaryDeleteWorkerHostedService> logger)
        {
            _deleteQueue = deleteQueue;
            _configuration = configuration;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                CloudinaryDeleteRequest request;
                try
                {
                    request = await _deleteQueue.DequeueAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }

                await ProcessDeleteRequestAsync(request, stoppingToken);
            }
        }

        private async Task ProcessDeleteRequestAsync(CloudinaryDeleteRequest request, CancellationToken cancellationToken)
        {
            for (var attempt = 1; attempt <= MaxDeleteAttempts; attempt++)
            {
                try
                {
                    var cloudinary = GetCloudinaryClient();
                    if (cloudinary == null)
                    {
                        return;
                    }

                    var deletionParams = CloudinaryService.BuildDeletionParams(request.PublicId, request.Type);
                    var result = await cloudinary.DestroyAsync(deletionParams);
                    if (result.Result == "ok" || result.Result == "not found")
                    {
                        return;
                    }

                    _logger.LogWarning(
                        "Cloudinary delete returned '{Result}' for PublicId '{PublicId}' (Attempt {Attempt}/{MaxAttempts}).",
                        result.Result,
                        request.PublicId,
                        attempt,
                        MaxDeleteAttempts);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Cloudinary delete failed for PublicId '{PublicId}' (Attempt {Attempt}/{MaxAttempts}).",
                        request.PublicId,
                        attempt,
                        MaxDeleteAttempts);
                }

                if (attempt < MaxDeleteAttempts)
                {
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(attempt), cancellationToken);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }
                }
            }

            _logger.LogError(
                "Cloudinary delete permanently failed for PublicId '{PublicId}' after {MaxAttempts} attempts.",
                request.PublicId,
                MaxDeleteAttempts);
        }

        private CloudinaryDotNet.Cloudinary? GetCloudinaryClient()
        {
            if (_cloudinary != null)
            {
                return _cloudinary;
            }

            var cloudName = _configuration["Cloudinary:CloudName"];
            var apiKey = _configuration["Cloudinary:ApiKey"];
            var apiSecret = _configuration["Cloudinary:ApiSecret"];

            if (string.IsNullOrWhiteSpace(cloudName) ||
                string.IsNullOrWhiteSpace(apiKey) ||
                string.IsNullOrWhiteSpace(apiSecret))
            {
                if (!_isCloudinaryConfigMissingLogged)
                {
                    _logger.LogWarning("Cloudinary delete worker is disabled because Cloudinary settings are missing.");
                    _isCloudinaryConfigMissingLogged = true;
                }
                return null;
            }

            _cloudinary = new CloudinaryDotNet.Cloudinary(new Account(cloudName, apiKey, apiSecret));
            return _cloudinary;
        }
    }
}
