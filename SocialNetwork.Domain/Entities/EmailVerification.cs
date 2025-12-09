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
        [Required, MaxLength(15)]
        [Column(TypeName = "varchar(15)")]
        public string Code { get; set; } = string.Empty;
        public DateTime ExpiredAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    }
}
