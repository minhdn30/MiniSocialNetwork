using AutoMapper;
using Google.Apis.Auth;
using Microsoft.AspNetCore.Http.HttpResults;
using SocialNetwork.Application.DTOs.AuthDTOs;
using SocialNetwork.Application.Exceptions;
using SocialNetwork.Application.Interfaces;
using SocialNetwork.Domain.Entities;
using SocialNetwork.Domain.Enums;
using SocialNetwork.Infrastructure.Repositories.Accounts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static SocialNetwork.Application.Exceptions.CustomExceptions;

namespace SocialNetwork.Application.Services
{
    public class AuthService : IAuthService
    {
        private readonly IAccountRepository _accountRepository;
        private readonly IMapper _mapper;
        private readonly IJwtService _jwtService;
        public AuthService(IAccountRepository accountRepository, IMapper mapper, IJwtService jwtService)
        {
            _accountRepository = accountRepository;
            _mapper = mapper;
            _jwtService = jwtService;
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
            account.IsEmailVerified = false;
            await _accountRepository.AddAccount(account);

            var accountMapped = _mapper.Map<RegisterResponse>(account);
            return accountMapped;
        }
        public async Task<LoginResponse?> LoginAsync(LoginRequest loginRequest)
        {
            var account = await _accountRepository.GetAccountByUsername(loginRequest.Username);
            if(account == null)
            {
                throw new UnauthorizedException("Invalid username or password.");
            }
            var isPasswordValid = BCrypt.Net.BCrypt.Verify(loginRequest.Password, account.PasswordHash);
            if(!isPasswordValid)
            {
                throw new UnauthorizedException("Invalid username or password.");
            }
            var accessToken = _jwtService.GenerateToken(account);

            // Tạo refresh token
            var refreshToken = Guid.NewGuid().ToString();
            account.RefreshToken = refreshToken;
            account.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
            account.LastActiveAt = DateTime.UtcNow;

            await _accountRepository.UpdateAccount(account);

            return new LoginResponse
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                RefreshTokenExpiryTime = account.RefreshTokenExpiryTime.Value
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

            // Sinh access token
            var accessToken = _jwtService.GenerateToken(account);

            // Tạo refresh token
            var refreshToken = Guid.NewGuid().ToString();
            account.RefreshToken = refreshToken;
            account.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
            account.LastActiveAt = DateTime.UtcNow;

            await _accountRepository.UpdateAccount(account);

            return new LoginResponse
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                RefreshTokenExpiryTime = account.RefreshTokenExpiryTime.Value
            };
        }
    }
}
