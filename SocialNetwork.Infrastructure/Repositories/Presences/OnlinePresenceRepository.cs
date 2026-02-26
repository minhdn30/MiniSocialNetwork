using Microsoft.EntityFrameworkCore;
using SocialNetwork.Domain.Enums;
using SocialNetwork.Infrastructure.Data;
using SocialNetwork.Infrastructure.Models;

namespace SocialNetwork.Infrastructure.Repositories.Presences
{
    public class OnlinePresenceRepository : IOnlinePresenceRepository
    {
        private readonly AppDbContext _context;

        public OnlinePresenceRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<PresenceSnapshotAccountStateModel>> GetSnapshotAccountStatesAsync(
            IReadOnlyCollection<Guid> accountIds,
            CancellationToken cancellationToken = default)
        {
            var normalizedIds = NormalizeIds(accountIds);
            if (normalizedIds.Count == 0)
            {
                return new List<PresenceSnapshotAccountStateModel>();
            }

            return await _context.Accounts
                .AsNoTracking()
                .Where(a => normalizedIds.Contains(a.AccountId))
                .Select(a => new PresenceSnapshotAccountStateModel
                {
                    AccountId = a.AccountId,
                    LastOnlineAt = a.LastOnlineAt,
                    Visibility = a.Settings != null
                        ? a.Settings.OnlineStatusVisibility
                        : OnlineStatusVisibilityEnum.ContactsOnly
                })
                .ToListAsync(cancellationToken);
        }

        public async Task<HashSet<Guid>> GetContactTargetIdsAsync(
            Guid viewerAccountId,
            IReadOnlyCollection<Guid> targetAccountIds,
            CancellationToken cancellationToken = default)
        {
            var normalizedTargetIds = NormalizeIds(targetAccountIds);
            if (viewerAccountId == Guid.Empty || normalizedTargetIds.Count == 0)
            {
                return new HashSet<Guid>();
            }

            var contactTargetIds = await _context.ConversationMembers
                .AsNoTracking()
                .Where(cm =>
                    cm.AccountId == viewerAccountId &&
                    !cm.HasLeft &&
                    !cm.IsDeleted &&
                    !cm.Conversation.IsDeleted)
                .SelectMany(cm => cm.Conversation.Members
                    .Where(m =>
                        !m.HasLeft &&
                        !m.IsDeleted &&
                        normalizedTargetIds.Contains(m.AccountId))
                    .Select(m => m.AccountId))
                .Distinct()
                .ToListAsync(cancellationToken);

            return contactTargetIds.ToHashSet();
        }

        public async Task<OnlineStatusVisibilityEnum?> GetOnlineStatusVisibilityAsync(
            Guid accountId,
            CancellationToken cancellationToken = default)
        {
            if (accountId == Guid.Empty)
            {
                return null;
            }

            return await _context.AccountSettings
                .AsNoTracking()
                .Where(s => s.AccountId == accountId)
                .Select(s => (OnlineStatusVisibilityEnum?)s.OnlineStatusVisibility)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<List<Guid>> GetAudienceAccountIdsAsync(
            Guid accountId,
            CancellationToken cancellationToken = default)
        {
            if (accountId == Guid.Empty)
            {
                return new List<Guid>();
            }

            return await _context.ConversationMembers
                .AsNoTracking()
                .Where(cm =>
                    cm.AccountId == accountId &&
                    !cm.HasLeft &&
                    !cm.IsDeleted &&
                    !cm.Conversation.IsDeleted)
                .SelectMany(cm => cm.Conversation.Members
                    .Where(m =>
                        !m.HasLeft &&
                        !m.IsDeleted &&
                        m.AccountId != accountId)
                    .Select(m => m.AccountId))
                .Distinct()
                .ToListAsync(cancellationToken);
        }

        public async Task<List<Guid>> UpdateLastOnlineAtAsync(
            IReadOnlyCollection<Guid> accountIds,
            DateTime lastOnlineAtUtc,
            CancellationToken cancellationToken = default)
        {
            var normalizedIds = NormalizeIds(accountIds);
            if (normalizedIds.Count == 0)
            {
                return new List<Guid>();
            }

            var existingIds = await _context.Accounts
                .AsNoTracking()
                .Where(a => normalizedIds.Contains(a.AccountId))
                .Select(a => a.AccountId)
                .ToListAsync(cancellationToken);

            if (existingIds.Count == 0)
            {
                return existingIds;
            }

            var utcTimestamp = DateTime.SpecifyKind(lastOnlineAtUtc, DateTimeKind.Utc);

            await _context.Accounts
                .Where(a => existingIds.Contains(a.AccountId))
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(a => a.LastOnlineAt, utcTimestamp),
                    cancellationToken);

            return existingIds;
        }

        private static List<Guid> NormalizeIds(IReadOnlyCollection<Guid>? ids)
        {
            return (ids ?? Array.Empty<Guid>())
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList();
        }
    }
}
