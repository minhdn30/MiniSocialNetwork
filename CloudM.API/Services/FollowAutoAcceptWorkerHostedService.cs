using CloudM.Application.Services.FollowServices;
using Microsoft.Extensions.Options;

namespace CloudM.API.Services
{
    public class FollowAutoAcceptWorkerHostedService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly FollowAutoAcceptOptions _options;
        private readonly ILogger<FollowAutoAcceptWorkerHostedService> _logger;

        public FollowAutoAcceptWorkerHostedService(
            IServiceScopeFactory scopeFactory,
            IOptions<FollowAutoAcceptOptions> options,
            ILogger<FollowAutoAcceptWorkerHostedService> logger)
        {
            _scopeFactory = scopeFactory;
            _options = (options?.Value ?? new FollowAutoAcceptOptions()).Normalize();
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                if (!_options.EnableWorker)
                {
                    await DelayAsync(_options.WorkerPollIntervalMs, stoppingToken);
                    continue;
                }

                try
                {
                    while (!stoppingToken.IsCancellationRequested)
                    {
                        using var scope = _scopeFactory.CreateScope();
                        var followAutoAcceptService = scope.ServiceProvider.GetRequiredService<IFollowAutoAcceptService>();
                        var processedCount = await followAutoAcceptService.ProcessPendingBatchAsync(
                            _options.BatchSize,
                            stoppingToken);
                        if (processedCount <= 0)
                        {
                            break;
                        }
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Follow auto-accept worker iteration failed.");
                }

                await DelayAsync(_options.WorkerPollIntervalMs, stoppingToken);
            }
        }

        private static async Task DelayAsync(int delayMs, CancellationToken cancellationToken)
        {
            var safeDelayMs = Math.Max(100, delayMs);
            await Task.Delay(safeDelayMs, cancellationToken);
        }
    }
}
