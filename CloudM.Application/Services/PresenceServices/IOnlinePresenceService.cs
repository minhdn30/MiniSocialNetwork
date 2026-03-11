using CloudM.Application.DTOs.PresenceDTOs;
using CloudM.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CloudM.Application.Services.PresenceServices
{
    public interface IOnlinePresenceService
    {
        Task MarkConnectedAsync(Guid accountId, string connectionId, DateTime nowUtc, CancellationToken cancellationToken = default);
        Task MarkDisconnectedAsync(Guid? accountId, string connectionId, DateTime nowUtc, CancellationToken cancellationToken = default);
        Task TouchHeartbeatAsync(Guid accountId, string connectionId, DateTime nowUtc, CancellationToken cancellationToken = default);
        Task<(bool Allowed, int RetryAfterSeconds)> TryConsumeSnapshotRateLimitAsync(
            Guid viewerAccountId,
            DateTime nowUtc,
            CancellationToken cancellationToken = default);
        Task<PresenceSnapshotResponse> GetSnapshotAsync(
            Guid viewerAccountId,
            IReadOnlyCollection<Guid> accountIds,
            DateTime nowUtc,
            CancellationToken cancellationToken = default);
        Task NotifyBlockedPairHiddenAsync(
            Guid currentId,
            Guid targetId,
            CancellationToken cancellationToken = default);
        Task NotifyVisibilityChangedAsync(
            Guid accountId,
            OnlineStatusVisibilityEnum previousVisibility,
            OnlineStatusVisibilityEnum currentVisibility,
            DateTime nowUtc,
            CancellationToken cancellationToken = default);
        Task<int> ProcessOfflineCandidatesAsync(DateTime nowUtc, int batchSize, CancellationToken cancellationToken = default);
    }
}
