using CloudM.Application.DTOs.SearchDTOs;

namespace CloudM.Application.Services.AccountSearchHistoryServices
{
    public interface IAccountSearchHistoryService
    {
        Task<List<SidebarAccountSearchResponse>> GetSidebarSearchHistoryAsync(
            Guid currentId,
            int limit = 12);
        Task SaveSidebarSearchHistoryAsync(Guid currentId, Guid targetId);
        Task DeleteSidebarSearchHistoryAsync(Guid currentId, Guid targetId);
    }
}
