using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Domain.Entities
{
    public class EmailVerification
    {
        public int Id { get; set; }
        [Required]
        [MaxLength(100)]
        [Column(TypeName = "varchar(100)")]
        public string Email { get; set; } = string.Empty;
        [Required, MaxLength(255)]
        [Column(TypeName = "varchar(255)")]
        public string CodeHash { get; set; } = string.Empty;
        [Required, MaxLength(64)]
        [Column(TypeName = "varchar(64)")]
        public string CodeSalt { get; set; } = string.Empty;
        public int FailedAttempts { get; set; } = 0;
        public DateTime LastSentAt { get; set; } = DateTime.UtcNow;
        public int SendCountInWindow { get; set; } = 0;
        public DateTime SendWindowStartedAt { get; set; } = DateTime.UtcNow;
        public int DailySendCount { get; set; } = 0;
        public DateTime DailyWindowStartedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LockedUntil { get; set; }
        public DateTime? ConsumedAt { get; set; }
        public DateTime ExpiredAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    }
}
