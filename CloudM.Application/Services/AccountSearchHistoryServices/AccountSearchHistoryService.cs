using CloudM.Application.DTOs.SearchDTOs;
using CloudM.Infrastructure.Models;
using CloudM.Infrastructure.Repositories.AccountSearchHistories;
using CloudM.Infrastructure.Repositories.UnitOfWork;
using static CloudM.Domain.Exceptions.CustomExceptions;

namespace CloudM.Application.Services.AccountSearchHistoryServices
{
    public class AccountSearchHistoryService : IAccountSearchHistoryService
    {
        private readonly IAccountSearchHistoryRepository _accountSearchHistoryRepository;
        private readonly IUnitOfWork _unitOfWork;

        public AccountSearchHistoryService(
            IAccountSearchHistoryRepository accountSearchHistoryRepository,
            IUnitOfWork unitOfWork)
        {
            _accountSearchHistoryRepository = accountSearchHistoryRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<List<SidebarAccountSearchResponse>> GetSidebarSearchHistoryAsync(
            Guid currentId,
            int limit = 12)
        {
            var results = await _accountSearchHistoryRepository.GetSidebarSearchHistoryAsync(currentId, limit);
            return results.Select(MapSidebarAccountSearchResponse).ToList();
        }

        public async Task SaveSidebarSearchHistoryAsync(Guid currentId, Guid targetId)
        {
            if (targetId == Guid.Empty || targetId == currentId)
            {
                throw new BadRequestException("Account is unavailable.");
            }

            var canUseTarget = await _accountSearchHistoryRepository.CanUseSidebarSearchTargetAsync(currentId, targetId);
            if (!canUseTarget)
            {
                throw new BadRequestException("Account is unavailable.");
            }

            await _accountSearchHistoryRepository.UpsertSidebarSearchHistoryAsync(currentId, targetId, DateTime.UtcNow);
            await _unitOfWork.CommitAsync();
        }

        public async Task DeleteSidebarSearchHistoryAsync(Guid currentId, Guid targetId)
        {
            if (targetId == Guid.Empty)
            {
                return;
            }

            await _accountSearchHistoryRepository.DeleteSidebarSearchHistoryAsync(currentId, targetId);
            await _unitOfWork.CommitAsync();
        }

        private static SidebarAccountSearchResponse MapSidebarAccountSearchResponse(
            SidebarAccountSearchModel item)
        {
            return new SidebarAccountSearchResponse
            {
                AccountId = item.AccountId,
                Username = item.Username,
                FullName = item.FullName,
                AvatarUrl = item.AvatarUrl,
                IsFollowing = item.IsFollowing,
                IsFollower = item.IsFollower,
                HasDirectConversation = item.HasDirectConversation,
                LastContactedAt = item.LastContactedAt,
                LastSearchedAt = item.LastSearchedAt
            };
        }
    }
}
