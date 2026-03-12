using CloudM.Infrastructure.Models;

namespace CloudM.Infrastructure.Repositories.AccountSearchHistories
{
    public interface IAccountSearchHistoryRepository
    {
        Task<List<SidebarAccountSearchModel>> GetSidebarSearchHistoryAsync(
            Guid currentId,
            int limit = 12);
        Task<bool> CanUseSidebarSearchTargetAsync(Guid currentId, Guid targetId);
        Task UpsertSidebarSearchHistoryAsync(Guid currentId, Guid targetId, DateTime searchedAt);
        Task DeleteSidebarSearchHistoryAsync(Guid currentId, Guid targetId);
    }
}
