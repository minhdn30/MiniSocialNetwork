namespace SocialNetwork.Application.Services.AuthServices
{
    public interface ILoginRateLimitService
    {
        Task EnforceLoginAllowedAsync(string email, string? ipAddress, DateTime nowUtc);
        Task RecordFailedAttemptAsync(string email, string? ipAddress, DateTime nowUtc);
        Task ClearFailedAttemptsAsync(string email, string? ipAddress, DateTime nowUtc);
    }
}
