using Microsoft.EntityFrameworkCore;
using Microsoft.VisualBasic;
using SocialNetwork.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Infrastructure.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public virtual DbSet<Account> Accounts { get; set; }
        public virtual DbSet<Role> Roles { get; set; }
        public virtual DbSet<EmailVerification> EmailVerifications { get; set; }
        public virtual DbSet<Follow> Follows { get; set; }
        public virtual DbSet<Post> Posts { get; set; }
        public virtual DbSet<PostMedia> PostMedias { get; set; }
        public virtual DbSet<PostReact> PostReacts { get; set; }
        public virtual DbSet<Comment> Comments { get; set; }
        public virtual DbSet<CommentReact> CommentReacts { get; set; }
        public virtual DbSet<Conversation> Conversations { get; set; }
        public virtual DbSet<ConversationMember> ConversationMembers { get; set; }
        public virtual DbSet<Message> Messages { get; set; }
        public virtual DbSet<MessageMedia> MessageMedias { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // =====================
            // ACCOUNT - ROLE
            // =====================
            modelBuilder.Entity<Account>(entity =>
            {
                entity.HasIndex(e => e.Email).IsUnique();
                entity.HasIndex(e => e.Username).IsUnique();

                entity.HasOne(a => a.Role)
                      .WithMany(r => r.Accounts)
                      .HasForeignKey(a => a.RoleId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Role>(entity =>
            {
                entity.HasIndex(e => e.RoleName).IsUnique();
            });

            // =====================
            // FOLLOW
            // =====================
            modelBuilder.Entity<Follow>()
                .HasKey(f => new { f.FollowerId, f.FollowedId });

            // Index for FollowedId to optimize "Who follows me" queries
            modelBuilder.Entity<Follow>()
                .HasIndex(f => f.FollowedId);

            modelBuilder.Entity<Follow>()
                .HasOne(f => f.Follower)
                .WithMany(a => a.Followings)
                .HasForeignKey(f => f.FollowerId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Follow>()
                .HasOne(f => f.Followed)
                .WithMany(a => a.Followers)
                .HasForeignKey(f => f.FollowedId)
                .OnDelete(DeleteBehavior.NoAction);

            // =====================
            // POST
            // =====================
            modelBuilder.Entity<Post>()
                .HasKey(p => p.PostId);
            //for Post in Feed
            modelBuilder.Entity<Post>()
                .HasIndex(p => new { p.IsDeleted, p.Privacy, p.CreatedAt })
                .HasDatabaseName("IX_Posts_Feed");
            //for Post in Profile
            modelBuilder.Entity<Post>()
                .HasIndex(p => new { p.AccountId, p.CreatedAt })
                .HasDatabaseName("IX_Posts_Account_CreatedAt");

            modelBuilder.Entity<Post>()
                .Property(p => p.Content)
                .HasMaxLength(5000);

            // Post → Account
            modelBuilder.Entity<Post>()
                .HasOne(p => p.Account)
                .WithMany(a => a.Posts)
                .HasForeignKey(p => p.AccountId)
                .OnDelete(DeleteBehavior.Cascade);

            // Post → Comments
            modelBuilder.Entity<Post>()
                .HasMany(p => p.Comments)
                .WithOne(c => c.Post)
                .HasForeignKey(c => c.PostId)
                .OnDelete(DeleteBehavior.Cascade);

            // Post → Medias
            modelBuilder.Entity<Post>()
                .HasMany(p => p.Medias)
                .WithOne(m => m.Post)
                .HasForeignKey(m => m.PostId)
                .OnDelete(DeleteBehavior.Cascade);

            // Post → Reacts
            modelBuilder.Entity<Post>()
                .HasMany(p => p.Reacts)
                .WithOne(r => r.Post)
                .HasForeignKey(r => r.PostId)
                .OnDelete(DeleteBehavior.Cascade);

            // =====================
            // POST MEDIA
            // =====================
            modelBuilder.Entity<PostMedia>()
                .HasKey(m => m.MediaId);

            // =====================
            // POST REACT
            // =====================
            modelBuilder.Entity<PostReact>()
                .HasKey(r => new { r.PostId, r.AccountId });

            modelBuilder.Entity<PostReact>()
                .HasIndex(r => r.PostId)
                .HasDatabaseName("IX_PostReact_PostId");

            // PostReact → Account
            modelBuilder.Entity<PostReact>()
                .HasOne(r => r.Account)
                .WithMany(a => a.PostReacts)
                .HasForeignKey(r => r.AccountId)
                .OnDelete(DeleteBehavior.Cascade);

            // PostReact → Post
            modelBuilder.Entity<PostReact>()
                .HasOne(r => r.Post)
                .WithMany(p => p.Reacts)
                .HasForeignKey(r => r.PostId)
                .OnDelete(DeleteBehavior.Cascade);


            // =====================
            // COMMENT
            // =====================
            modelBuilder.Entity<Comment>()
                .HasKey(c => c.CommentId);

            modelBuilder.Entity<Comment>()
                .HasIndex(r => r.PostId)
                .HasDatabaseName("IX_Comment_PostId");

            // Comment → Account
            modelBuilder.Entity<Comment>()
                .HasOne(c => c.Account)
                .WithMany(a => a.Comments)
                .HasForeignKey(c => c.AccountId)
                .OnDelete(DeleteBehavior.Cascade);

            // Comment → Post
            modelBuilder.Entity<Comment>()
                .HasOne(c => c.Post)
                .WithMany(p => p.Comments)
                .HasForeignKey(c => c.PostId)
                .OnDelete(DeleteBehavior.Cascade);

            // Comment Reply (Self-Reference)
            modelBuilder.Entity<Comment>()
                .HasOne(c => c.ParentComment)
                .WithMany(c => c.Replies)
                .HasForeignKey(c => c.ParentCommentId)
                .OnDelete(DeleteBehavior.SetNull); // tránh cycle

            // =====================
            // COMMENT REACT
            // =====================
            modelBuilder.Entity<CommentReact>()
                .HasKey(cr => new { cr.CommentId, cr.AccountId });

            modelBuilder.Entity<CommentReact>()
                .HasOne(cr => cr.Comment)
                .WithMany(c => c.CommentReacts)
                .HasForeignKey(cr => cr.CommentId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CommentReact>()
                .HasOne(cr => cr.Account)
                .WithMany(a => a.CommentReacts)
                .HasForeignKey(cr => cr.AccountId)
                .OnDelete(DeleteBehavior.Cascade);

            // =====================
            // CONVERSATION
            // =====================
            modelBuilder.Entity<Conversation>()
                .HasKey(c => c.ConversationId);

            // Conversation → Creator (Account)
            modelBuilder.Entity<Conversation>()
                .HasOne(c => c.CreatedByAccount)
                .WithMany(a => a.CreatedConversations)
                .HasForeignKey(c => c.CreatedBy)
                .OnDelete(DeleteBehavior.Restrict);

            // Index: sort / filter conversations
            modelBuilder.Entity<Conversation>()
                .HasIndex(c => c.CreatedAt);

            modelBuilder.Entity<Conversation>()
                .HasIndex(c => c.CreatedBy);

            // Conversation → Members
            modelBuilder.Entity<Conversation>()
                .HasMany(c => c.Members)
                .WithOne(m => m.Conversation)
                .HasForeignKey(m => m.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);

            // Conversation → Messages
            modelBuilder.Entity<Conversation>()
                .HasMany(c => c.Messages)
                .WithOne(m => m.Conversation)
                .HasForeignKey(m => m.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);



            // =====================
            // CONVERSATION MEMBER
            // =====================
            modelBuilder.Entity<ConversationMember>()
                .HasKey(cm => new { cm.ConversationId, cm.AccountId });

            // Index: get conversations by account
            modelBuilder.Entity<ConversationMember>()
                .HasIndex(cm => cm.AccountId);

            // Index: get members by conversation
            modelBuilder.Entity<ConversationMember>()
                .HasIndex(cm => cm.ConversationId);

            // ConversationMember → Account
            modelBuilder.Entity<ConversationMember>()
                .HasOne(cm => cm.Account)
                .WithMany(a => a.Conversations)
                .HasForeignKey(cm => cm.AccountId)
                .OnDelete(DeleteBehavior.Cascade);

            // ConversationMember → Conversation
            modelBuilder.Entity<ConversationMember>()
                .HasOne(cm => cm.Conversation)
                .WithMany(c => c.Members)
                .HasForeignKey(cm => cm.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);



            // =====================
            // MESSAGE
            // =====================
            modelBuilder.Entity<Message>()
                .HasKey(m => m.MessageId);

            // MOST IMPORTANT index for chat message loading
            modelBuilder.Entity<Message>()
                .HasIndex(m => new { m.ConversationId, m.SentAt });

            // Index for sender-based filtering / audit
            modelBuilder.Entity<Message>()
                .HasIndex(m => m.AccountId);

            // Message → Conversation
            modelBuilder.Entity<Message>()
                .HasOne(m => m.Conversation)
                .WithMany(c => c.Messages)
                .HasForeignKey(m => m.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);

            // Message → Account (Sender)
            modelBuilder.Entity<Message>()
                .HasOne(m => m.Account)
                .WithMany(a => a.Messages)
                .HasForeignKey(m => m.AccountId)
                .OnDelete(DeleteBehavior.Restrict);



            // =====================
            // MESSAGE MEDIA
            // =====================
            modelBuilder.Entity<MessageMedia>()
                .HasKey(mm => mm.MessageMediaId);

            // Index: load media by message
            modelBuilder.Entity<MessageMedia>()
                .HasIndex(mm => mm.MessageId);

            modelBuilder.Entity<MessageMedia>()
                .HasOne(mm => mm.Message)
                .WithMany(m => m.Medias)
                .HasForeignKey(mm => mm.MessageId)
                .OnDelete(DeleteBehavior.Cascade);




        }

    }
}
