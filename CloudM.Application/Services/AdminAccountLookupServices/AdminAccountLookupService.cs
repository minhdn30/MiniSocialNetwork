using CloudM.Application.DTOs.AdminDTOs;
using CloudM.Domain.Enums;
using CloudM.Infrastructure.Repositories.AdminAccountLookups;

namespace CloudM.Application.Services.AdminAccountLookupServices
{
    public class AdminAccountLookupService : IAdminAccountLookupService
    {
        private const int LookupLimit = 20;
        private const int MaxKeywordLength = 100;

        private readonly IAdminAccountLookupRepository _adminAccountLookupRepository;

        public AdminAccountLookupService(IAdminAccountLookupRepository adminAccountLookupRepository)
        {
            _adminAccountLookupRepository = adminAccountLookupRepository;
        }

        public async Task<AdminAccountLookupResponse> LookupAccountsAsync(AdminAccountLookupRequest request)
        {
            var normalizedKeyword = NormalizeKeyword(request.Keyword);
            if (!IsKeywordEligible(normalizedKeyword))
            {
                return new AdminAccountLookupResponse
                {
                    Keyword = normalizedKeyword
                };
            }

            var items = await _adminAccountLookupRepository.LookupAccountsAsync(normalizedKeyword, LookupLimit);

            return new AdminAccountLookupResponse
            {
                Keyword = normalizedKeyword,
                TotalResults = items.Count,
                Items = items.Select(item => new AdminAccountLookupItemResponse
                {
                    AccountId = item.AccountId,
                    Username = item.Username,
                    Fullname = item.FullName,
                    Email = item.Email,
                    AvatarUrl = item.AvatarUrl,
                    Role = item.RoleName,
                    Status = item.Status.ToString(),
                    IsEmailVerified = item.Status != AccountStatusEnum.EmailNotVerified,
                    CreatedAt = item.CreatedAt,
                    LastOnlineAt = item.LastOnlineAt,
                }).ToList()
            };
        }

        private static string NormalizeKeyword(string keyword)
        {
            var normalizedKeyword = (keyword ?? string.Empty).Trim();
            if (normalizedKeyword.Length <= MaxKeywordLength)
            {
                return normalizedKeyword;
            }

            return normalizedKeyword[..MaxKeywordLength];
        }

        private static bool IsKeywordEligible(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return false;
            }

            if (Guid.TryParse(keyword, out _))
            {
                return true;
            }

            if (keyword.Contains('@'))
            {
                return keyword.Length >= 3;
            }

            return keyword.Length >= 3;
        }
    }
}
