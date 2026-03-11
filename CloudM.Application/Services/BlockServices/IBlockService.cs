using CloudM.Application.DTOs.BlockDTOs;
using CloudM.Application.DTOs.CommonDTOs;

namespace CloudM.Application.Services.BlockServices
{
    public interface IBlockService
    {
        Task<BlockStatusResponse> BlockAsync(Guid currentId, Guid targetId, CancellationToken cancellationToken = default);
        Task<BlockStatusResponse> UnblockAsync(Guid currentId, Guid targetId, CancellationToken cancellationToken = default);
        Task<BlockStatusResponse> GetStatusAsync(Guid currentId, Guid targetId, CancellationToken cancellationToken = default);
        Task<PagedResponse<BlockedAccountListItemResponse>> GetBlockedAccountsAsync(
            Guid currentId,
            BlockedAccountListRequest request,
            CancellationToken cancellationToken = default);
    }
}
