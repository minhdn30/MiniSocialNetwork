using AutoMapper;
using Google.Apis.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity.Data;
using SocialNetwork.Application.DTOs.AuthDTOs;
using SocialNetwork.Domain.Exceptions;
using SocialNetwork.Application.Services.JwtServices;
using SocialNetwork.Domain.Entities;
using SocialNetwork.Domain.Enums;
using SocialNetwork.Infrastructure.Repositories.Accounts;
using SocialNetwork.Infrastructure.Repositories.AccountSettingRepos;
using SocialNetwork.Infrastructure.Repositories.UnitOfWork;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using static SocialNetwork.Domain.Exceptions.CustomExceptions;
using LoginRequest = SocialNetwork.Application.DTOs.AuthDTOs.LoginRequest;

namespace SocialNetwork.Application.Services.AuthServices
{
    public class AuthService : IAuthService
    {
        private readonly IAccountRepository _accountRepository;
        private readonly IAccountSettingRepository _accountSettingRepository;
        private readonly IMapper _mapper;
        private readonly IJwtService _jwtService;
        private readonly IUnitOfWork _unitOfWork;

        public AuthService(IAccountRepository accountRepository, 
            IAccountSettingRepository accountSettingRepository,
            IMapper mapper, IJwtService jwtService, IUnitOfWork unitOfWork)
        {
            _accountRepository = accountRepository;
            _accountSettingRepository = accountSettingRepository;
            _mapper = mapper;
            _jwtService = jwtService;
            _unitOfWork = unitOfWork;
        }
        public async Task<RegisterResponse> RegisterAsync(RegisterDTO registerRequest)
        {
            var usernameExists = await _accountRepository.IsUsernameExist(registerRequest.Username);
            if(usernameExists)
            {
                throw new BadRequestException("Username already exists.");
            }
            var emailExists = await _accountRepository.IsEmailExist(registerRequest.Email);
            if(emailExists)
            {
                throw new BadRequestException("Email already exists.");
            }
            var account = _mapper.Map<Account>(registerRequest);
            account.PasswordHash = BCrypt.Net.BCrypt.HashPassword(registerRequest.Password);
            account.RoleId = (int)RoleEnum.User;
            account.Status = AccountStatusEnum.EmailNotVerified;
            account.Settings ??= new AccountSettings
            {
                AccountId = account.AccountId
            };
            await _accountRepository.AddAccount(account);
            await _unitOfWork.CommitAsync();

            var accountMapped = _mapper.Map<RegisterResponse>(account);
            return accountMapped;
        }
        public async Task<LoginResponse?> LoginAsync(LoginRequest loginRequest)
        {
            var account = await _accountRepository.GetAccountByEmail(loginRequest.Email);
            if(account == null)
            {
                throw new UnauthorizedException("Invalid email or password.");
            }
            var isPasswordValid = BCrypt.Net.BCrypt.Verify(loginRequest.Password, account.PasswordHash);
            if(!isPasswordValid)
            {
                throw new UnauthorizedException("Invalid email or password.");
            }
            
            if (account.Status == AccountStatusEnum.Banned || account.Status == AccountStatusEnum.Suspended || account.Status == AccountStatusEnum.Deleted)
            {
                throw new UnauthorizedException("Your account has been restricted. Please contact support.");
            }
            if (account.Status == AccountStatusEnum.EmailNotVerified)
            {
                throw new UnauthorizedException("Email is not verified. Please verify your email.");
            }
            //create access token
            var accessToken = _jwtService.GenerateToken(account);
            //create refresh token
            var refreshToken = Guid.NewGuid().ToString();
            account.RefreshToken = refreshToken;
            account.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);

            await _accountRepository.UpdateAccount(account);
            await _unitOfWork.CommitAsync();

            var settings = await _accountSettingRepository.GetGetAccountSettingsByAccountIdAsync(account.AccountId);
            var defaultPostPrivacy = settings != null ? settings.DefaultPostPrivacy : PostPrivacyEnum.Public;

            return new LoginResponse
            {
                AccountId = account.AccountId,
                Fullname = account.FullName,
                Username = account.Username,
                AvatarUrl = account.AvatarUrl,
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                RefreshTokenExpiryTime = account.RefreshTokenExpiryTime.Value,
                Status = account.Status,
                DefaultPostPrivacy = defaultPostPrivacy
            };
        }
        public async Task<LoginResponse> LoginWithGoogleAsync(string idToken)
        {
            // Xác thực token với Google
            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, new GoogleJsonWebSignature.ValidationSettings());
            var email = payload.Email;

            // Kiểm tra xem account có tồn tại không
            var account = await _accountRepository.GetAccountByEmail(email);
            if (account == null)
            {
                throw new UnauthorizedException("Account not registered. Please sign up first.");
            }

            if (account.Status == AccountStatusEnum.Banned || account.Status == AccountStatusEnum.Suspended || account.Status == AccountStatusEnum.Deleted)
            {
                throw new UnauthorizedException("Your account has been restricted. Please contact support.");
            }
            if (account.Status == AccountStatusEnum.EmailNotVerified)
            {
                throw new UnauthorizedException("Email is not verified. Please verify your email.");
            }

            // Sinh access token
            var accessToken = _jwtService.GenerateToken(account);

            // Tạo refresh token
            var refreshToken = Guid.NewGuid().ToString();
            account.RefreshToken = refreshToken;
            account.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
            account.LastActiveAt = DateTime.UtcNow;

            await _accountRepository.UpdateAccount(account);
            await _unitOfWork.CommitAsync();

            var settings = await _accountSettingRepository.GetGetAccountSettingsByAccountIdAsync(account.AccountId);
            var defaultPostPrivacy = settings != null ? settings.DefaultPostPrivacy : PostPrivacyEnum.Public;

            return new LoginResponse
            {
                AccountId = account.AccountId,
                Fullname = account.FullName,
                Username = account.Username,
                AvatarUrl = account.AvatarUrl,
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                RefreshTokenExpiryTime = account.RefreshTokenExpiryTime.Value,
                Status = account.Status,
                DefaultPostPrivacy = defaultPostPrivacy
            };
        }
        public async Task<LoginResponse?> RefreshTokenAsync(string refreshToken)
        {
            if (string.IsNullOrEmpty(refreshToken))
                throw new UnauthorizedException("No refresh token provided.");

            var account = await _accountRepository.GetByRefreshToken(refreshToken);
            if (account == null || account.RefreshTokenExpiryTime <= DateTime.UtcNow)
                throw new UnauthorizedException("Invalid or expired refresh token.");

            if (account.Status == AccountStatusEnum.Banned || account.Status == AccountStatusEnum.Suspended || account.Status == AccountStatusEnum.Deleted)
                throw new UnauthorizedException("Your account has been restricted.");
            if (account.Status == AccountStatusEnum.EmailNotVerified)
                throw new UnauthorizedException("Email is not verified. Please verify your email.");

            var newAccessToken = _jwtService.GenerateToken(account);

            var newRefreshToken = Convert.ToBase64String(
                RandomNumberGenerator.GetBytes(64)
            );

            account.RefreshToken = newRefreshToken;
            account.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
            account.UpdatedAt = DateTime.UtcNow;

            await _accountRepository.UpdateAccount(account);
            await _unitOfWork.CommitAsync();

            var settings = await _accountSettingRepository.GetGetAccountSettingsByAccountIdAsync(account.AccountId);
            var defaultPostPrivacy = settings != null ? settings.DefaultPostPrivacy : PostPrivacyEnum.Public;

            return new LoginResponse
            {
                AccountId = account.AccountId,
                Username = account.Username,
                AccessToken = newAccessToken,
                RefreshToken = newRefreshToken,
                RefreshTokenExpiryTime = account.RefreshTokenExpiryTime.Value,
                Fullname = account.FullName,
                AvatarUrl = account.AvatarUrl,
                Status = account.Status,
                DefaultPostPrivacy = defaultPostPrivacy
            };
        }

        public async Task LogoutAsync(Guid accountId, HttpResponse response)
        {
            var account = await _accountRepository.GetAccountById(accountId);
            if (account == null)
                throw new NotFoundException("Account not found.");

            account.RefreshToken = null;
            account.RefreshTokenExpiryTime = null;
            await _accountRepository.UpdateAccount(account);
            await _unitOfWork.CommitAsync();

            response.Cookies.Delete("refreshToken");
        }
    }
}
