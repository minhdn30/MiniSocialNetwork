namespace SocialNetwork.Application.Services.EmailVerificationServices
{
    public interface IEmailVerificationRateLimitService
    {
        Task EnforceSendRateLimitAsync(string email, string? ipAddress, DateTime nowUtc);
    }
}
