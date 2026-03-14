using Microsoft.EntityFrameworkCore;
using CloudM.Domain.Entities;
using CloudM.Infrastructure.Data;
using CloudM.Infrastructure.Models;

namespace CloudM.Infrastructure.Repositories.AdminAuditLogs
{
    public class AdminAuditLogRepository : IAdminAuditLogRepository
    {
        private readonly AppDbContext _context;

        public AdminAuditLogRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(AdminAuditLog adminAuditLog)
        {
            await _context.AdminAuditLogs.AddAsync(adminAuditLog);
        }

        public async Task<List<AdminAuditLogItemModel>> GetRecentLogsAsync(string module, string actionType, int limit)
        {
            var normalizedModule = (module ?? string.Empty).Trim();
            var normalizedActionType = (actionType ?? string.Empty).Trim();
            var safeLimit = Math.Clamp(limit, 1, 30);

            return await _context.AdminAuditLogs
                .AsNoTracking()
                .Where(log =>
                    (string.IsNullOrWhiteSpace(normalizedModule) || log.Module == normalizedModule) &&
                    (string.IsNullOrWhiteSpace(normalizedActionType) || log.ActionType == normalizedActionType))
                .OrderByDescending(log => log.CreatedAt)
                .ThenByDescending(log => log.AdminAuditLogId)
                .Take(safeLimit)
                .Select(log => new AdminAuditLogItemModel
                {
                    AdminAuditLogId = log.AdminAuditLogId,
                    AdminId = log.AdminId,
                    AdminEmail = log.Admin.Email,
                    AdminFullname = log.Admin.FullName,
                    Module = log.Module,
                    ActionType = log.ActionType,
                    TargetType = log.TargetType,
                    TargetId = log.TargetId,
                    Summary = log.Summary,
                    RequestIp = log.RequestIp,
                    CreatedAt = log.CreatedAt,
                })
                .ToListAsync();
        }
    }
}
