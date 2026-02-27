using Microsoft.AspNetCore.Http;
using CloudM.Application.DTOs.AuthDTOs;
using CloudM.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CloudM.Application.Services.AuthServices
{
    public interface IAuthService
    {
        Task<RegisterResponse> RegisterAsync(RegisterDTO registerRequest);
        Task<LoginResponse?> LoginAsync(LoginRequest loginRequest, string? requesterIpAddress = null);
        Task<ExternalLoginStartResponse> StartExternalLoginAsync(ExternalLoginProviderEnum provider, string credential);
        Task<LoginResponse> CompleteExternalProfileAsync(
            ExternalLoginProviderEnum provider,
            string credential,
            string username,
            string fullName);
        Task<LoginResponse> LoginWithExternalAsync(ExternalLoginProviderEnum provider, string credential);
        Task<LoginResponse> LoginWithGoogleAsync(string idToken);
        Task<IReadOnlyList<ExternalLoginSummaryResponse>> GetExternalLoginsAsync(Guid accountId);
        Task UnlinkExternalLoginAsync(Guid accountId, ExternalLoginProviderEnum provider);
        Task SetPasswordAsync(Guid accountId, string newPassword, string confirmPassword);
        Task<LoginResponse?> RefreshTokenAsync(string refreshToken);
        Task LogoutAsync(Guid accountId, HttpResponse response);
    }
}
