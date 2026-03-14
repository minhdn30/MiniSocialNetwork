using CloudM.Domain.Entities;
using CloudM.Infrastructure.Models;

namespace CloudM.Infrastructure.Repositories.AdminAuditLogs
{
    public interface IAdminAuditLogRepository
    {
        Task AddAsync(AdminAuditLog adminAuditLog);
        Task<List<AdminAuditLogItemModel>> GetRecentLogsAsync(string module, string actionType, int limit);
    }
}
