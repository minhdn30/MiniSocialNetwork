using Microsoft.Extensions.Options;
using SocialNetwork.Application.Services.EmailVerificationServices;
using SocialNetwork.Infrastructure.Repositories.EmailVerifications;
using SocialNetwork.Infrastructure.Repositories.UnitOfWork;

namespace SocialNetwork.API.Services
{
    public class EmailVerificationCleanupHostedService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<EmailVerificationCleanupHostedService> _logger;
        private readonly EmailVerificationSecurityOptions _securityOptions;

        public EmailVerificationCleanupHostedService(
            IServiceScopeFactory scopeFactory,
            ILogger<EmailVerificationCleanupHostedService> logger,
            IOptions<EmailVerificationSecurityOptions> securityOptions)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _securityOptions = securityOptions.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var intervalMinutes = _securityOptions.CleanupIntervalMinutes <= 0
                ? 30
                : _securityOptions.CleanupIntervalMinutes;

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var emailVerificationRepository = scope.ServiceProvider.GetRequiredService<IEmailVerificationRepository>();
                    var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

                    var nowUtc = DateTime.UtcNow;
                    var createdBeforeUtc = nowUtc.AddHours(-Math.Max(1, _securityOptions.RetentionHours));
                    var removedEmailRows = await emailVerificationRepository.CleanupStaleVerificationsAsync(nowUtc, createdBeforeUtc);

                    if (removedEmailRows > 0)
                    {
                        await unitOfWork.CommitAsync();
                        _logger.LogInformation(
                            "EmailVerification cleanup removed {RemovedEmailRows} stale rows.",
                            removedEmailRows);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "EmailVerification cleanup job failed.");
                }

                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(intervalMinutes), stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }
    }
}
