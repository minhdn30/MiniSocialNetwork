namespace CloudM.Application.Services.AuthServices
{
    public interface IPasswordResetService
    {
        Task SendResetPasswordCodeAsync(string email, string? requesterIpAddress = null);
        Task<bool> VerifyResetPasswordCodeAsync(string email, string code);
        Task ResetPasswordAsync(string email, string code, string newPassword, string confirmPassword);
    }
}
