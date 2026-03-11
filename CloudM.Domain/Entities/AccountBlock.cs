using System.ComponentModel.DataAnnotations;

namespace CloudM.Domain.Entities
{
    public class AccountBlock
    {
        public Guid BlockerId { get; set; }
        public Guid BlockedId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [MaxLength(100)]
        public string? BlockerSnapshotUsername { get; set; }

        [MaxLength(100)]
        public string? BlockedSnapshotUsername { get; set; }

        public virtual Account Blocker { get; set; } = null!;
        public virtual Account Blocked { get; set; } = null!;
    }
}
