using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SocialNetwork.Domain.Entities
{
    public class EmailVerificationIpRateLimit
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(64)]
        [Column(TypeName = "varchar(64)")]
        public string IpAddress { get; set; } = string.Empty;

        public DateTime LastSentAt { get; set; } = DateTime.UnixEpoch;
        public int SendCountInWindow { get; set; } = 0;
        public DateTime SendWindowStartedAt { get; set; } = DateTime.UtcNow;
        public int DailySendCount { get; set; } = 0;
        public DateTime DailyWindowStartedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LockedUntil { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
