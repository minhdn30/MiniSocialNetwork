using Microsoft.AspNetCore.Http;
using SocialNetwork.Application.DTOs.AuthDTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Application.Services.AuthServices
{
    public interface IAuthService
    {
        Task<RegisterResponse> RegisterAsync(RegisterDTO registerRequest);
        Task<LoginResponse?> LoginAsync(LoginRequest loginRequest, string? requesterIpAddress = null);
        Task<LoginResponse> LoginWithGoogleAsync(string idToken);
        Task<LoginResponse?> RefreshTokenAsync(string refreshToken);
        Task LogoutAsync(Guid accountId, HttpResponse response);
    }
}
