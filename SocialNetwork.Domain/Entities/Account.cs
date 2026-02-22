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
        public Guid AccountId { get; set; }
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
        [MaxLength(500)]
        public string? CoverUrl { get; set; }
        [MaxLength(500)]
        public string? Bio { get; set; }
        [MaxLength(15)]
        public string? Phone { get; set; }
        public bool? Gender { get; set; }
        public string? PasswordHash { get; set; }
        [MaxLength(255)]
        public string? Address { get; set; }
        public int RoleId { get; set; }
        public AccountStatusEnum Status { get; set; } = 0;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        [MaxLength(256)]
        public string? RefreshToken { get; set; }
        public DateTime? RefreshTokenExpiryTime { get; set; }
        public DateTime? LastOnlineAt { get; set; }
        public virtual Role Role { get; set; } = null!;
        public ICollection<Follow> Followers { get; set; } = new List<Follow>(); // Accounts that follow this account
        public ICollection<Follow> Followings { get; set; } = new List<Follow>(); // Accounts that this account follows
        public virtual ICollection<Post> Posts { get; set; } = new List<Post>();
        public virtual ICollection<ExternalLogin> ExternalLogins { get; set; } = new List<ExternalLogin>();

        // Comment-related
        public virtual ICollection<Comment> Comments { get; set; } = new List<Comment>();

        // Reacts
        public virtual ICollection<PostReact> PostReacts { get; set; } = new List<PostReact>();
        public virtual ICollection<CommentReact> CommentReacts { get; set; } = new List<CommentReact>();
        //chat
        public virtual ICollection<ConversationMember> Conversations { get; set; } = new List<ConversationMember>();
        public virtual ICollection<Message> Messages { get; set; } = new List<Message>();
        public virtual ICollection<Conversation> CreatedConversations { get; set; } = new List<Conversation>();
        public virtual ICollection<Conversation> OwnedConversations { get; set; } = new List<Conversation>();
        public virtual AccountSettings Settings { get; set; } = null!;

    }
}
