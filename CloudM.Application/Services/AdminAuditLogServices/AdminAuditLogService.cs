using CloudM.Application.DTOs.AdminDTOs;
using CloudM.Domain.Entities;
using CloudM.Infrastructure.Repositories.AdminAuditLogs;

namespace CloudM.Application.Services.AdminAuditLogServices
{
    public class AdminAuditLogService : IAdminAuditLogService
    {
        private const int DefaultLimit = 12;
        private const int MaxLimit = 30;

        private readonly IAdminAuditLogRepository _adminAuditLogRepository;

        public AdminAuditLogService(IAdminAuditLogRepository adminAuditLogRepository)
        {
            _adminAuditLogRepository = adminAuditLogRepository;
        }

        public async Task RecordLoginAsync(Guid adminId, string? requesterIpAddress)
        {
            await RecordAsync(new AdminAuditLogWriteRequest
            {
                AdminId = adminId,
                Module = "auth",
                ActionType = "AdminLogin",
                TargetType = "AdminPortal",
                TargetId = adminId.ToString(),
                Summary = "Admin signed in to the admin portal",
                RequestIp = requesterIpAddress,
            });
        }

        public async Task RecordAsync(AdminAuditLogWriteRequest request)
        {
            var adminAuditLog = new AdminAuditLog
            {
                AdminId = request.AdminId,
                Module = NormalizeString(request.Module, 100),
                ActionType = NormalizeString(request.ActionType, 100),
                TargetType = NormalizeNullableString(request.TargetType, 100),
                TargetId = NormalizeNullableString(request.TargetId, 100),
                Summary = NormalizeString(request.Summary, 300),
                RequestIp = NormalizeIp(request.RequestIp),
                CreatedAt = DateTime.UtcNow,
            };

            await _adminAuditLogRepository.AddAsync(adminAuditLog);
        }

        public async Task<AdminAuditLogResponse> GetRecentLogsAsync(AdminAuditLogQueryRequest request)
        {
            var appliedLimit = NormalizeLimit(request.Limit);
            var normalizedModule = NormalizeFilter(request.Module);
            var normalizedActionType = NormalizeFilter(request.ActionType);
            var items = await _adminAuditLogRepository.GetRecentLogsAsync(
                normalizedModule,
                normalizedActionType,
                appliedLimit);

            return new AdminAuditLogResponse
            {
                TotalResults = items.Count,
                AppliedLimit = appliedLimit,
                Module = normalizedModule,
                ActionType = normalizedActionType,
                Items = items.Select(item => new AdminAuditLogItemResponse
                {
                    AdminAuditLogId = item.AdminAuditLogId,
                    AdminId = item.AdminId,
                    AdminEmail = item.AdminEmail,
                    AdminFullname = item.AdminFullname,
                    Module = item.Module,
                    ActionType = item.ActionType,
                    TargetType = item.TargetType,
                    TargetId = item.TargetId,
                    Summary = item.Summary,
                    RequestIp = item.RequestIp,
                    CreatedAt = item.CreatedAt,
                }).ToList()
            };
        }

        private static int NormalizeLimit(int limit)
        {
            if (limit <= 0)
            {
                return DefaultLimit;
            }

            return Math.Clamp(limit, 1, MaxLimit);
        }

        private static string NormalizeFilter(string value)
        {
            return (value ?? string.Empty).Trim();
        }

        private static string NormalizeString(string value, int maxLength)
        {
            var normalizedValue = (value ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalizedValue))
            {
                return string.Empty;
            }

            return normalizedValue.Length <= maxLength
                ? normalizedValue
                : normalizedValue[..maxLength];
        }

        private static string? NormalizeNullableString(string? value, int maxLength)
        {
            var normalizedValue = NormalizeString(value ?? string.Empty, maxLength);
            return string.IsNullOrWhiteSpace(normalizedValue) ? null : normalizedValue;
        }

        private static string? NormalizeIp(string? requesterIpAddress)
        {
            var normalizedIp = (requesterIpAddress ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalizedIp))
            {
                return null;
            }

            return normalizedIp.Length <= 64 ? normalizedIp : normalizedIp[..64];
        }
    }
}
