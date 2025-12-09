using SocialNetwork.Domain.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Domain.Entities
{
    public class Account
    {
        public Guid AccountId { get; set; } = Guid.NewGuid();
        [Required, MaxLength(100)]
        [Column(TypeName = "varchar(100)")]
        public string Username { get; set; } = null!;
        [Required, MaxLength(100)]
        [Column(TypeName = "varchar(100)")]
        public string Email { get; set; } = null!;
        [Required, MaxLength(100)]
        [Column(TypeName = "varchar(100)")]
        public string FullName { get; set; } = null!;
        [MaxLength(255)]
        public string? AvatarUrl { get; set; }
        [MaxLength(15)]
        public string? Phone { get; set; }
        public bool? Gender { get; set; }
        [Required]
        public string PasswordHash { get; set; } = null!;
        [MaxLength(255)]
        public string? Address { get; set; }
        public int RoleId { get; set; }
        public bool Status { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public bool IsEmailVerified { get; set; } = false;
        [MaxLength(256)]
        public string? RefreshToken { get; set; }
        public DateTime? RefreshTokenExpiryTime { get; set; }
        public DateTime? LastActiveAt { get; set; }
        public virtual Role Role { get; set; } = null!;
        public ICollection<Follow> Followers { get; set; } = new List<Follow>(); // Accounts that follow this account
        public ICollection<Follow> Followings { get; set; } = new List<Follow>(); // Accounts that this account follows
        public virtual ICollection<Post> Posts { get; set; } = new List<Post>();

        // Comment-related
        public virtual ICollection<Comment> Comments { get; set; } = new List<Comment>();

        // Reacts
        public virtual ICollection<PostReact> PostReacts { get; set; } = new List<PostReact>();
        public virtual ICollection<CommentReact> CommentReacts { get; set; } = new List<CommentReact>();



    }
}
