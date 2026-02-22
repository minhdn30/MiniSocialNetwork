using SocialNetwork.Domain.Enums;
using SocialNetwork.Infrastructure.Models;

namespace SocialNetwork.Infrastructure.Repositories.Presences
{
    public interface IOnlinePresenceRepository
    {
        Task<List<PresenceSnapshotAccountStateModel>> GetSnapshotAccountStatesAsync(
            IReadOnlyCollection<Guid> accountIds,
            CancellationToken cancellationToken = default);

        Task<HashSet<Guid>> GetContactTargetIdsAsync(
            Guid viewerAccountId,
            IReadOnlyCollection<Guid> targetAccountIds,
            CancellationToken cancellationToken = default);

        Task<OnlineStatusVisibilityEnum?> GetOnlineStatusVisibilityAsync(
            Guid accountId,
            CancellationToken cancellationToken = default);

        Task<List<Guid>> GetAudienceAccountIdsAsync(
            Guid accountId,
            CancellationToken cancellationToken = default);

        Task<List<Guid>> UpdateLastOnlineAtAsync(
            IReadOnlyCollection<Guid> accountIds,
            DateTime lastOnlineAtUtc,
            CancellationToken cancellationToken = default);
    }
}
