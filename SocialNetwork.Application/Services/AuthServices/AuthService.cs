using AutoMapper;
using Microsoft.AspNetCore.Http;
using SocialNetwork.Application.DTOs.AuthDTOs;
using SocialNetwork.Application.Services.JwtServices;
using SocialNetwork.Domain.Entities;
using SocialNetwork.Domain.Enums;
using SocialNetwork.Infrastructure.Repositories.Accounts;
using SocialNetwork.Infrastructure.Repositories.AccountSettingRepos;
using SocialNetwork.Infrastructure.Repositories.ExternalLogins;
using SocialNetwork.Infrastructure.Repositories.UnitOfWork;
using System.Net;
using System.Security.Cryptography;
using static SocialNetwork.Domain.Exceptions.CustomExceptions;
using LoginRequest = SocialNetwork.Application.DTOs.AuthDTOs.LoginRequest;

namespace SocialNetwork.Application.Services.AuthServices
{
    public class AuthService : IAuthService
    {
        private const int MaxFullNameLength = 100;
        private const int ExternalProfileFullNameMaxLength = 25;

        private readonly IAccountRepository _accountRepository;
        private readonly IExternalLoginRepository _externalLoginRepository;
        private readonly IAccountSettingRepository _accountSettingRepository;
        private readonly IMapper _mapper;
        private readonly IJwtService _jwtService;
        private readonly ILoginRateLimitService _loginRateLimitService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly Dictionary<ExternalLoginProviderEnum, IExternalIdentityProvider> _externalIdentityProviders;

        public AuthService(
            IAccountRepository accountRepository,
            IExternalLoginRepository externalLoginRepository,
            IAccountSettingRepository accountSettingRepository,
            IMapper mapper,
            IJwtService jwtService,
            ILoginRateLimitService loginRateLimitService,
            IUnitOfWork unitOfWork,
            IEnumerable<IExternalIdentityProvider> externalIdentityProviders)
        {
            _accountRepository = accountRepository;
            _externalLoginRepository = externalLoginRepository;
            _accountSettingRepository = accountSettingRepository;
            _mapper = mapper;
            _jwtService = jwtService;
            _loginRateLimitService = loginRateLimitService;
            _unitOfWork = unitOfWork;
            _externalIdentityProviders = externalIdentityProviders
                .GroupBy(x => x.Provider)
                .ToDictionary(x => x.Key, x => x.First());
        }

        public async Task<RegisterResponse> RegisterAsync(RegisterDTO registerRequest)
        {
            var normalizedUsername = NormalizeUsername(registerRequest.Username);
            var normalizedEmail = NormalizeEmail(registerRequest.Email);

            var usernameExists = await _accountRepository.IsUsernameExist(normalizedUsername);
            if (usernameExists)
            {
                throw new BadRequestException("Username already exists.");
            }

            var emailExists = await _accountRepository.IsEmailExist(normalizedEmail);
            if (emailExists)
            {
                throw new BadRequestException("Email already exists.");
            }

            registerRequest.Username = normalizedUsername;
            registerRequest.Email = normalizedEmail;

            var account = _mapper.Map<Account>(registerRequest);
            account.Username = normalizedUsername;
            account.Email = normalizedEmail;
            account.PasswordHash = BCrypt.Net.BCrypt.HashPassword(registerRequest.Password);
            account.RoleId = (int)RoleEnum.User;
            account.Status = AccountStatusEnum.EmailNotVerified;
            account.Settings ??= new AccountSettings
            {
                AccountId = account.AccountId
            };

            await _accountRepository.AddAccount(account);
            await _unitOfWork.CommitAsync();

            return _mapper.Map<RegisterResponse>(account);
        }

        public async Task<LoginResponse?> LoginAsync(LoginRequest loginRequest, string? requesterIpAddress = null)
        {
            var normalizedEmail = NormalizeEmail(loginRequest.Email);
            var normalizedIpAddress = NormalizeIp(requesterIpAddress);
            var nowUtc = DateTime.UtcNow;

            try
            {
                await _loginRateLimitService.EnforceLoginAllowedAsync(normalizedEmail, normalizedIpAddress, nowUtc);
            }
            catch (InternalServerException)
            {
                // Keep login flow available even when external rate-limit storage is unavailable.
            }

            var account = await _accountRepository.GetAccountByEmail(normalizedEmail);
            if (account == null)
            {
                await RecordLoginFailureAsync(normalizedEmail, normalizedIpAddress, nowUtc);
                throw new UnauthorizedException("Invalid email or password.");
            }

            await EnsureAccountHasAuthMethodOrThrowAsync(account);

            if (!HasPassword(account))
            {
                await RecordLoginFailureAsync(normalizedEmail, normalizedIpAddress, nowUtc);
                throw new UnauthorizedException("Invalid email or password.");
            }

            var isPasswordValid = BCrypt.Net.BCrypt.Verify(loginRequest.Password, account.PasswordHash);
            if (!isPasswordValid)
            {
                await RecordLoginFailureAsync(normalizedEmail, normalizedIpAddress, nowUtc);
                throw new UnauthorizedException("Invalid email or password.");
            }

            try
            {
                await _loginRateLimitService.ClearFailedAttemptsAsync(normalizedEmail, normalizedIpAddress, nowUtc);
            }
            catch (InternalServerException)
            {
                // Best-effort only.
            }

            EnsureAccountCanLoginWithPassword(account);

            account.RefreshToken = GenerateRefreshToken();
            account.RefreshTokenExpiryTime = nowUtc.AddDays(7);
            account.LastActiveAt = nowUtc;
            account.UpdatedAt = nowUtc;

            await _accountRepository.UpdateAccount(account);
            await _unitOfWork.CommitAsync();

            return await BuildLoginResponseAsync(account);
        }

        public Task<LoginResponse> LoginWithGoogleAsync(string idToken)
        {
            return LoginWithExternalAsync(ExternalLoginProviderEnum.Google, idToken);
        }

        public async Task<LoginResponse> LoginWithExternalAsync(ExternalLoginProviderEnum provider, string credential)
        {
            var startResult = await StartExternalLoginAsync(provider, credential);
            if (startResult.RequiresProfileCompletion || startResult.Login == null)
            {
                throw new BadRequestException("Please complete username and full name to continue.");
            }

            return startResult.Login;
        }

        public Task<ExternalLoginStartResponse> StartExternalLoginAsync(
            ExternalLoginProviderEnum provider,
            string credential)
        {
            return HandleExternalLoginAsync(
                provider,
                credential,
                requestedUsername: null,
                requestedFullName: null,
                allowCreateWhenMissingAccount: false);
        }

        public async Task<LoginResponse> CompleteExternalProfileAsync(
            ExternalLoginProviderEnum provider,
            string credential,
            string username,
            string fullName)
        {
            var startResult = await HandleExternalLoginAsync(
                provider,
                credential,
                requestedUsername: username,
                requestedFullName: fullName,
                allowCreateWhenMissingAccount: true);

            if (startResult.Login == null)
            {
                throw new InternalServerException("Unable to complete external sign-in.");
            }

            return startResult.Login;
        }

        private async Task<ExternalLoginStartResponse> HandleExternalLoginAsync(
            ExternalLoginProviderEnum provider,
            string credential,
            string? requestedUsername,
            string? requestedFullName,
            bool allowCreateWhenMissingAccount)
        {
            var verification = await VerifyExternalIdentityAsync(provider, credential);
            var nowUtc = DateTime.UtcNow;

            return await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                var externalLogin = await _externalLoginRepository.GetByProviderUserIdAsync(
                    provider,
                    verification.NormalizedProviderUserId);
                Account account;
                var isNewAccount = false;

                if (externalLogin != null)
                {
                    account = externalLogin.Account
                        ?? await _accountRepository.GetAccountById(externalLogin.AccountId)
                        ?? throw new UnauthorizedException("Unable to sign in with this account.");
                }
                else
                {
                    var existingAccount = await _accountRepository.GetAccountByEmail(verification.NormalizedEmail);
                    if (existingAccount == null)
                    {
                        if (!allowCreateWhenMissingAccount)
                        {
                            var suggestedUsername = await GenerateUniqueUsernameAsync(
                                BuildUsernameBase(verification.Identity, verification.NormalizedEmail));
                            var suggestedFullName = BuildDisplayName(
                                verification.Identity,
                                verification.NormalizedEmail,
                                suggestedUsername);

                            return new ExternalLoginStartResponse
                            {
                                RequiresProfileCompletion = true,
                                Profile = new ExternalProfilePrefillResponse
                                {
                                    Provider = provider.ToString(),
                                    Email = verification.NormalizedEmail,
                                    SuggestedUsername = suggestedUsername,
                                    SuggestedFullName = LimitLength(suggestedFullName, ExternalProfileFullNameMaxLength),
                                    AvatarUrl = null
                                }
                            };
                        }

                        account = await CreateExternalAccountAsync(
                            verification.Identity,
                            verification.NormalizedEmail,
                            nowUtc,
                            requestedUsername,
                            requestedFullName);
                        await _accountRepository.AddAccount(account);
                        isNewAccount = true;
                    }
                    else
                    {
                        account = existingAccount;
                    }

                    EnsureAccountAllowedForExternalLink(account);

                    if (account.Status == AccountStatusEnum.EmailNotVerified)
                    {
                        account.Status = AccountStatusEnum.Active;
                    }

                    externalLogin = new ExternalLogin
                    {
                        Id = Guid.NewGuid(),
                        AccountId = account.AccountId,
                        Provider = provider,
                        ProviderUserId = verification.NormalizedProviderUserId,
                        CreatedAt = nowUtc,
                        LastLoginAt = nowUtc
                    };

                    await _externalLoginRepository.AddAsync(externalLogin);
                }

                await EnsureAccountHasAuthMethodOrThrowAsync(
                    account,
                    hasPendingExternalLogin: externalLogin != null);
                EnsureAccountCanLoginWithExternal(account);

                var resolvedExternalLogin = externalLogin
                    ?? throw new InternalServerException("Unable to establish external login.");
                resolvedExternalLogin.LastLoginAt = nowUtc;

                account.LastActiveAt = nowUtc;
                account.RefreshToken = GenerateRefreshToken();
                account.RefreshTokenExpiryTime = nowUtc.AddDays(7);
                account.UpdatedAt = nowUtc;
                if (!isNewAccount)
                {
                    await _accountRepository.UpdateAccount(account);
                }

                return new ExternalLoginStartResponse
                {
                    RequiresProfileCompletion = false,
                    Login = await BuildLoginResponseAsync(account)
                };
            });
        }

        private async Task<VerifiedExternalIdentity> VerifyExternalIdentityAsync(
            ExternalLoginProviderEnum provider,
            string credential)
        {
            if (!_externalIdentityProviders.TryGetValue(provider, out var identityProvider))
            {
                throw new BadRequestException($"Provider '{provider}' is not supported.");
            }

            var identity = await identityProvider.VerifyAsync(credential);
            if (identity.Provider != provider)
            {
                throw new BadRequestException("External provider does not match credential.");
            }

            if (!identity.EmailVerified)
            {
                throw new UnauthorizedException("Provider email is not verified.");
            }

            var normalizedProviderUserId = NormalizeProviderUserId(identity.ProviderUserId);
            if (string.IsNullOrWhiteSpace(normalizedProviderUserId))
            {
                throw new UnauthorizedException("External account identifier is invalid.");
            }

            var normalizedEmail = NormalizeEmail(identity.Email);
            if (string.IsNullOrWhiteSpace(normalizedEmail))
            {
                throw new UnauthorizedException("Provider email is invalid.");
            }

            return new VerifiedExternalIdentity(
                identity,
                normalizedProviderUserId,
                normalizedEmail);
        }

        public async Task<IReadOnlyList<ExternalLoginSummaryResponse>> GetExternalLoginsAsync(Guid accountId)
        {
            var externalLogins = await _externalLoginRepository.GetByAccountIdAsync(accountId);
            return externalLogins
                .Select(x => new ExternalLoginSummaryResponse
                {
                    Provider = x.Provider.ToString(),
                    CreatedAt = x.CreatedAt,
                    LastLoginAt = x.LastLoginAt,
                })
                .ToList();
        }

        public async Task UnlinkExternalLoginAsync(Guid accountId, ExternalLoginProviderEnum provider)
        {
            var account = await _accountRepository.GetAccountById(accountId);
            if (account == null)
            {
                throw new NotFoundException("Account not found.");
            }

            var externalLogin = await _externalLoginRepository.GetByAccountIdAndProviderAsync(accountId, provider);
            if (externalLogin == null)
            {
                throw new NotFoundException("External login is not linked.");
            }

            var externalLoginCount = await _externalLoginRepository.CountByAccountIdAsync(accountId);
            if (externalLoginCount <= 1 && !HasPassword(account))
            {
                throw new BadRequestException("Please set a password before unlinking your last external login.");
            }

            await _externalLoginRepository.DeleteAsync(externalLogin);
            await _unitOfWork.CommitAsync();
        }

        public async Task SetPasswordAsync(Guid accountId, string newPassword, string confirmPassword)
        {
            var account = await _accountRepository.GetAccountById(accountId);
            if (account == null)
            {
                throw new NotFoundException("Account not found.");
            }

            account.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            account.UpdatedAt = DateTime.UtcNow;

            await _accountRepository.UpdateAccount(account);
            await _unitOfWork.CommitAsync();
        }

        public async Task<LoginResponse?> RefreshTokenAsync(string refreshToken)
        {
            var account = await _accountRepository.GetByRefreshToken(refreshToken);
            if (account == null || account.RefreshTokenExpiryTime <= DateTime.UtcNow)
            {
                throw new UnauthorizedException("Invalid or expired refresh token.");
            }

            if (account.Status == AccountStatusEnum.Banned || account.Status == AccountStatusEnum.Suspended || account.Status == AccountStatusEnum.Deleted)
            {
                throw new UnauthorizedException("Your account has been restricted.");
            }

            if (account.Status == AccountStatusEnum.EmailNotVerified)
            {
                throw new UnauthorizedException("Email is not verified. Please verify your email.");
            }

            await EnsureAccountHasAuthMethodOrThrowAsync(account);

            var newAccessToken = _jwtService.GenerateToken(account);
            var newRefreshToken = GenerateRefreshToken();

            account.RefreshToken = newRefreshToken;
            account.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
            account.UpdatedAt = DateTime.UtcNow;
            await _accountRepository.UpdateAccount(account);
            await _unitOfWork.CommitAsync();

            var settings = await _accountSettingRepository.GetGetAccountSettingsByAccountIdAsync(account.AccountId);
            var defaultPostPrivacy = settings?.DefaultPostPrivacy ?? PostPrivacyEnum.Public;

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
            {
                throw new NotFoundException("Account not found.");
            }

            account.RefreshToken = null;
            account.RefreshTokenExpiryTime = null;
            await _accountRepository.UpdateAccount(account);
            await _unitOfWork.CommitAsync();

            response.Cookies.Delete("refreshToken");
        }

        private async Task<Account> CreateExternalAccountAsync(
            ExternalAuthIdentity identity,
            string normalizedEmail,
            DateTime nowUtc,
            string? requestedUsername,
            string? requestedFullName)
        {
            var normalizedRequestedUsername = NormalizeUsername(requestedUsername ?? string.Empty);
            if (string.IsNullOrWhiteSpace(normalizedRequestedUsername))
            {
                throw new BadRequestException("Username is required.");
            }

            if (await _accountRepository.IsUsernameExist(normalizedRequestedUsername))
            {
                throw new BadRequestException("Username already exists.");
            }

            var normalizedRequestedFullName = NormalizeFullName(requestedFullName);
            if (string.IsNullOrWhiteSpace(normalizedRequestedFullName))
            {
                throw new BadRequestException("Full name is required.");
            }

            var account = new Account
            {
                AccountId = Guid.NewGuid(),
                Username = normalizedRequestedUsername,
                Email = normalizedEmail,
                FullName = LimitLength(normalizedRequestedFullName, MaxFullNameLength),
                AvatarUrl = null,
                PasswordHash = null,
                RoleId = (int)RoleEnum.User,
                Status = AccountStatusEnum.Active,
                CreatedAt = nowUtc,
                UpdatedAt = nowUtc,
                Settings = new AccountSettings()
            };

            account.Settings.AccountId = account.AccountId;
            return account;
        }

        private async Task<string> GenerateUniqueUsernameAsync(string baseUsername)
        {
            var normalizedBase = NormalizeUsername(baseUsername);
            if (string.IsNullOrWhiteSpace(normalizedBase))
            {
                normalizedBase = $"user{RandomNumberGenerator.GetInt32(100000, 999999)}";
            }

            if (!await _accountRepository.IsUsernameExist(normalizedBase))
            {
                return normalizedBase;
            }

            for (var attempt = 0; attempt < 30; attempt++)
            {
                var candidate = $"{normalizedBase}{RandomNumberGenerator.GetInt32(1000, 9999)}";
                if (!await _accountRepository.IsUsernameExist(candidate))
                {
                    return candidate;
                }
            }

            return $"user{RandomNumberGenerator.GetInt32(10000000, 99999999)}";
        }

        private static string BuildUsernameBase(ExternalAuthIdentity identity, string normalizedEmail)
        {
            var emailName = normalizedEmail.Split('@')[0];
            var source = !string.IsNullOrWhiteSpace(emailName)
                ? emailName
                : identity.FullName ?? "user";

            var filtered = new string(source
                .Trim()
                .ToLowerInvariant()
                .Where(ch => char.IsLetterOrDigit(ch) || ch == '_')
                .ToArray());

            if (filtered.Length < 6)
            {
                filtered = $"user{RandomNumberGenerator.GetInt32(1000, 9999)}";
            }

            if (filtered.Length > 30)
            {
                filtered = filtered[..30];
            }

            return filtered;
        }

        private static string BuildDisplayName(ExternalAuthIdentity identity, string normalizedEmail, string username)
        {
            if (!string.IsNullOrWhiteSpace(identity.FullName))
            {
                return LimitLength(identity.FullName.Trim(), MaxFullNameLength);
            }

            var emailName = normalizedEmail.Split('@')[0].Trim();
            if (!string.IsNullOrWhiteSpace(emailName))
            {
                return LimitLength(emailName, MaxFullNameLength);
            }

            return LimitLength(username, MaxFullNameLength);
        }

        private static string LimitLength(string value, int maxLength)
        {
            if (value.Length <= maxLength)
            {
                return value;
            }

            return value[..maxLength];
        }

        private static bool HasPassword(Account account)
        {
            return !string.IsNullOrWhiteSpace(account.PasswordHash);
        }

        private async Task EnsureAccountHasAuthMethodOrThrowAsync(
            Account account,
            bool hasPendingExternalLogin = false)
        {
            if (HasPassword(account) || hasPendingExternalLogin)
            {
                return;
            }

            var externalLoginCount = await _externalLoginRepository.CountByAccountIdAsync(account.AccountId);
            if (externalLoginCount > 0)
            {
                return;
            }

            throw new InternalServerException("Account is missing authentication methods. Please contact support.");
        }

        private static void EnsureAccountAllowedForExternalLink(Account account)
        {
            if (account.Status == AccountStatusEnum.Banned || account.Status == AccountStatusEnum.Suspended || account.Status == AccountStatusEnum.Deleted)
            {
                throw new UnauthorizedException("Your account has been restricted. Please contact support.");
            }
        }

        private static void EnsureAccountCanLoginWithExternal(Account account)
        {
            EnsureAccountAllowedForExternalLink(account);

            if (account.Status == AccountStatusEnum.EmailNotVerified)
            {
                throw new UnauthorizedException("Email is not verified. Please verify your email.");
            }
        }

        private static void EnsureAccountCanLoginWithPassword(Account account)
        {
            if (account.Status == AccountStatusEnum.Banned || account.Status == AccountStatusEnum.Suspended || account.Status == AccountStatusEnum.Deleted)
            {
                throw new UnauthorizedException("Your account has been restricted. Please contact support.");
            }

            if (account.Status == AccountStatusEnum.EmailNotVerified)
            {
                throw new UnauthorizedException("Email is not verified. Please verify your email.");
            }
        }

        private async Task RecordLoginFailureAsync(string normalizedEmail, string? normalizedIpAddress, DateTime nowUtc)
        {
            try
            {
                await _loginRateLimitService.RecordFailedAttemptAsync(normalizedEmail, normalizedIpAddress, nowUtc);
            }
            catch (InternalServerException)
            {
                // Best-effort only.
            }
        }

        private async Task<LoginResponse> BuildLoginResponseAsync(Account account)
        {
            var accessToken = _jwtService.GenerateToken(account);
            var settings = account.Settings ?? await _accountSettingRepository.GetGetAccountSettingsByAccountIdAsync(account.AccountId);
            var defaultPostPrivacy = settings != null ? settings.DefaultPostPrivacy : PostPrivacyEnum.Public;

            return new LoginResponse
            {
                AccountId = account.AccountId,
                Fullname = account.FullName,
                Username = account.Username,
                AvatarUrl = account.AvatarUrl,
                AccessToken = accessToken,
                RefreshToken = account.RefreshToken,
                RefreshTokenExpiryTime = account.RefreshTokenExpiryTime ?? DateTime.UtcNow.AddDays(7),
                Status = account.Status,
                DefaultPostPrivacy = defaultPostPrivacy
            };
        }

        private static string NormalizeUsername(string username)
        {
            return (username ?? string.Empty).Trim().ToLowerInvariant();
        }

        private static string NormalizeEmail(string email)
        {
            return (email ?? string.Empty).Trim().ToLowerInvariant();
        }

        private static string NormalizeFullName(string? fullName)
        {
            return (fullName ?? string.Empty).Trim();
        }

        private static string NormalizeProviderUserId(string providerUserId)
        {
            return (providerUserId ?? string.Empty).Trim();
        }

        private static string? NormalizeIp(string? ipAddress)
        {
            if (string.IsNullOrWhiteSpace(ipAddress))
            {
                return null;
            }

            var candidate = ipAddress.Split(',')[0].Trim();
            if (!IPAddress.TryParse(candidate, out var parsedIp))
            {
                return null;
            }

            if (parsedIp.IsIPv4MappedToIPv6)
            {
                parsedIp = parsedIp.MapToIPv4();
            }

            return parsedIp.ToString();
        }

        private static string GenerateRefreshToken()
        {
            return Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        }

        private sealed class VerifiedExternalIdentity
        {
            public ExternalAuthIdentity Identity { get; }
            public string NormalizedProviderUserId { get; }
            public string NormalizedEmail { get; }

            public VerifiedExternalIdentity(
                ExternalAuthIdentity identity,
                string normalizedProviderUserId,
                string normalizedEmail)
            {
                Identity = identity;
                NormalizedProviderUserId = normalizedProviderUserId;
                NormalizedEmail = normalizedEmail;
            }
        }
    }
}
