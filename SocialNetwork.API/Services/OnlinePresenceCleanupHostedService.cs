using Microsoft.Extensions.Options;
using SocialNetwork.Application.Services.PresenceServices;

namespace SocialNetwork.API.Services
{
    public class OnlinePresenceCleanupHostedService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<OnlinePresenceCleanupHostedService> _logger;
        private readonly OnlinePresenceOptions _options;

        public OnlinePresenceCleanupHostedService(
            IServiceScopeFactory scopeFactory,
            ILogger<OnlinePresenceCleanupHostedService> logger,
            IOptions<OnlinePresenceOptions> options)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _options = (options.Value ?? new OnlinePresenceOptions()).Normalize();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var delaySeconds = Math.Max(1, _options.WorkerIntervalSeconds);
            var batchSize = Math.Max(1, _options.WorkerBatchSize);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var presenceService = scope.ServiceProvider.GetRequiredService<IOnlinePresenceService>();

                    await presenceService.ProcessOfflineCandidatesAsync(
                        DateTime.UtcNow,
                        batchSize,
                        stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Online presence cleanup worker failed.");
                }

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
    }
}
