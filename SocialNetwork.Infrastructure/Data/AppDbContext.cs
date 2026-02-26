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
        public static string Unaccent(string text) => throw new NotSupportedException();
        public static double Similarity(string source, string target) => throw new NotSupportedException();
        public virtual DbSet<Account> Accounts { get; set; }
        public virtual DbSet<Role> Roles { get; set; }
        public virtual DbSet<ExternalLogin> ExternalLogins { get; set; }
        public virtual DbSet<EmailVerification> EmailVerifications { get; set; }
        public virtual DbSet<Follow> Follows { get; set; }
        public virtual DbSet<Post> Posts { get; set; }
        public virtual DbSet<PostMedia> PostMedias { get; set; }
        public virtual DbSet<PostReact> PostReacts { get; set; }
        public virtual DbSet<Story> Stories { get; set; }
        public virtual DbSet<StoryView> StoryViews { get; set; }
        public virtual DbSet<Comment> Comments { get; set; }
        public virtual DbSet<CommentReact> CommentReacts { get; set; }
        public virtual DbSet<Conversation> Conversations { get; set; }
        public virtual DbSet<ConversationMember> ConversationMembers { get; set; }
        public virtual DbSet<Message> Messages { get; set; }
        public virtual DbSet<MessageMedia> MessageMedias { get; set; }
        public virtual DbSet<MessageHidden> MessageHiddens { get; set; }
        public virtual DbSet<MessageReact> MessageReacts { get; set; }
        public virtual DbSet<PinnedMessage> PinnedMessages { get; set; }
        public virtual DbSet<AccountSettings> AccountSettings { get; set; } = null!;


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

                // Trigram index for fast partial search on FullName
                entity.HasIndex(e => e.FullName)
                      .HasMethod("GIN")
                      .HasOperators("gin_trgm_ops");

                // Expression indexes for unaccent(FullName) and unaccent(Username) 
                // will be created manually via raw SQL in migration
                // EF Core doesn't support functional indexes via Fluent API


                // Index for Status filter (used in many joins)
                entity.HasIndex(e => e.Status)
                      .HasDatabaseName("IX_Accounts_Status");

                entity.HasOne(a => a.Role)
                      .WithMany(r => r.Accounts)
                      .HasForeignKey(a => a.RoleId)
                      .OnDelete(DeleteBehavior.Restrict);

                // Account - AccountSettings (1:1)
                entity.HasOne(a => a.Settings)
                      .WithOne(s => s.Account)
                      .HasForeignKey<AccountSettings>(s => s.AccountId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<ExternalLogin>(entity =>
            {
                entity.HasIndex(e => new { e.Provider, e.ProviderUserId })
                      .IsUnique()
                      .HasDatabaseName("IX_ExternalLogins_Provider_ProviderUserId_Unique");

                entity.HasIndex(e => new { e.AccountId, e.Provider })
                      .IsUnique()
                      .HasDatabaseName("IX_ExternalLogins_AccountId_Provider_Unique");

                entity.HasIndex(e => e.AccountId)
                      .HasDatabaseName("IX_ExternalLogins_AccountId");

                entity.HasOne(e => e.Account)
                      .WithMany(a => a.ExternalLogins)
                      .HasForeignKey(e => e.AccountId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Enable extensions
            modelBuilder.HasPostgresExtension("pg_trgm");
            modelBuilder.HasPostgresExtension("unaccent");

            // Map to immutable_unaccent wrapper function (created in migration)
            // Required for functional indexes - PostgreSQL requires IMMUTABLE functions for indexes
            modelBuilder.HasDbFunction(typeof(AppDbContext).GetMethod(nameof(Unaccent), new[] { typeof(string) })!)
                .HasName("immutable_unaccent");
            modelBuilder.HasDbFunction(typeof(AppDbContext).GetMethod(nameof(Similarity), new[] { typeof(string), typeof(string) })!)
                .HasName("similarity");



            modelBuilder.Entity<Role>(entity =>
            {
                entity.HasIndex(e => e.RoleName).IsUnique();
            });

            modelBuilder.Entity<EmailVerification>(entity =>
            {
                entity.HasIndex(e => e.Email)
                    .IsUnique()
                    .HasDatabaseName("IX_EmailVerifications_Email_Unique");

                entity.HasIndex(e => e.ExpiredAt)
                    .HasDatabaseName("IX_EmailVerifications_ExpiredAt");
            });

            // =====================
            // FOLLOW
            // =====================
            modelBuilder.Entity<Follow>()
                .HasKey(f => new { f.FollowerId, f.FollowedId });

            // Index for FollowedId to optimize "Who follows me" queries
            // Index for FollowedId to optimize "Who follows me" queries
            // Also include CreatedAt for efficient sorting
            modelBuilder.Entity<Follow>()
                .HasIndex(f => new { f.FollowedId, f.CreatedAt });

            // Index for FollowerId + CreatedAt for efficient sorting of "Following" list
            modelBuilder.Entity<Follow>()
                .HasIndex(f => new { f.FollowerId, f.CreatedAt });

            // Composite index for fast relationship checks
            modelBuilder.Entity<Follow>()
                .HasIndex(f => new { f.FollowerId, f.FollowedId })
                .HasDatabaseName("IX_Follow_Follower_Followed");

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
                .HasIndex(p => new { p.AccountId, p.IsDeleted, p.CreatedAt })
                .HasDatabaseName("IX_Posts_Account_CreatedAt");

            modelBuilder.Entity<Post>()
                .Property(p => p.Content)
                .HasMaxLength(5000);

            modelBuilder.Entity<Post>()
                .Property(p => p.PostCode)
                .IsRequired()
                .HasMaxLength(12);

            modelBuilder.Entity<Post>()
                .HasIndex(p => p.PostCode)
                .IsUnique();

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
                .HasKey(r => new { r.PostId, r.AccountId });

            // Covering index for post detail queries (count reacts, check if reacted)
            modelBuilder.Entity<PostReact>()
                .HasIndex(r => r.PostId)
                .HasDatabaseName("IX_PostReact_PostId_Covering");

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
            // STORY
            // =====================
            modelBuilder.Entity<Story>()
                .HasKey(s => s.StoryId);

            modelBuilder.Entity<Story>()
                .Property(s => s.TextContent)
                .HasMaxLength(1000);

            modelBuilder.Entity<Story>()
                .Property(s => s.BackgroundColorKey)
                .HasMaxLength(100);

            modelBuilder.Entity<Story>()
                .Property(s => s.FontTextKey)
                .HasMaxLength(100);

            modelBuilder.Entity<Story>()
                .Property(s => s.FontSizeKey)
                .HasMaxLength(100);

            modelBuilder.Entity<Story>()
                .Property(s => s.TextColorKey)
                .HasMaxLength(100);

            modelBuilder.Entity<Story>()
                .HasIndex(s => new { s.IsDeleted, s.ExpiresAt, s.CreatedAt })
                .HasDatabaseName("IX_Stories_Active");

            modelBuilder.Entity<Story>()
                .HasIndex(s => new { s.AccountId, s.IsDeleted, s.ExpiresAt, s.CreatedAt })
                .HasDatabaseName("IX_Stories_Account_Archive");

            modelBuilder.Entity<Story>()
                .HasIndex(s => new { s.AccountId, s.CreatedAt })
                .HasDatabaseName("IX_Stories_Account_Created");

            modelBuilder.Entity<Story>()
                .ToTable(table =>
                {
                    table.HasCheckConstraint(
                        "CK_Stories_ExpiresAt",
                        "\"ExpiresAt\" > \"CreatedAt\"");

                    table.HasCheckConstraint(
                        "CK_Stories_ContentPayload",
                        "((\"ContentType\" IN (0,1) AND \"MediaUrl\" IS NOT NULL AND \"TextContent\" IS NULL AND \"FontTextKey\" IS NULL AND \"FontSizeKey\" IS NULL AND \"TextColorKey\" IS NULL) OR (\"ContentType\" = 2 AND \"TextContent\" IS NOT NULL AND length(btrim(\"TextContent\")) > 0 AND \"MediaUrl\" IS NULL))");
                });

            modelBuilder.Entity<Story>()
                .HasOne(s => s.Account)
                .WithMany(a => a.Stories)
                .HasForeignKey(s => s.AccountId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Story>()
                .HasMany(s => s.Views)
                .WithOne(v => v.Story)
                .HasForeignKey(v => v.StoryId)
                .OnDelete(DeleteBehavior.Cascade);

            // =====================
            // STORY VIEW
            // =====================
            modelBuilder.Entity<StoryView>()
                .HasKey(v => new { v.StoryId, v.ViewerAccountId });

            modelBuilder.Entity<StoryView>()
                .HasIndex(v => new { v.StoryId, v.ViewedAt })
                .HasDatabaseName("IX_StoryViews_Story_ViewedAt");

            modelBuilder.Entity<StoryView>()
                .HasIndex(v => new { v.StoryId, v.ReactType })
                .HasDatabaseName("IX_StoryViews_Story_ReactType");

            modelBuilder.Entity<StoryView>()
                .HasIndex(v => new { v.ViewerAccountId, v.ViewedAt })
                .HasDatabaseName("IX_StoryViews_Viewer_ViewedAt");

            // Index for viewer + story existence checks (used in story ring unseen computation)
            modelBuilder.Entity<StoryView>()
                .HasIndex(v => new { v.ViewerAccountId, v.StoryId })
                .HasDatabaseName("IX_StoryViews_Viewer_Story");

            modelBuilder.Entity<StoryView>()
                .ToTable(table =>
                {
                    table.HasCheckConstraint(
                        "CK_StoryViews_ReactPair",
                        "((\"ReactType\" IS NULL AND \"ReactedAt\" IS NULL) OR (\"ReactType\" IS NOT NULL AND \"ReactedAt\" IS NOT NULL))");
                });

            modelBuilder.Entity<StoryView>()
                .HasOne(v => v.Story)
                .WithMany(s => s.Views)
                .HasForeignKey(v => v.StoryId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<StoryView>()
                .HasOne(v => v.ViewerAccount)
                .WithMany(a => a.StoryViews)
                .HasForeignKey(v => v.ViewerAccountId)
                .OnDelete(DeleteBehavior.Cascade);


            // =====================
            // COMMENT
            // =====================
            modelBuilder.Entity<Comment>()
                .HasKey(c => c.CommentId);

            modelBuilder.Entity<Comment>()
                .HasIndex(c => new { c.PostId, c.ParentCommentId, c.CreatedAt })
                .HasDatabaseName("IX_Comment_Post_Parent_Created");

            // Index for per-post interactions by account (feed affinity query)
            modelBuilder.Entity<Comment>()
                .HasIndex(c => new { c.PostId, c.AccountId, c.CreatedAt })
                .HasDatabaseName("IX_Comment_Post_Account_Created");

            modelBuilder.Entity<Comment>()
                .HasIndex(c => new { c.ParentCommentId, c.CreatedAt })
                .HasDatabaseName("IX_Comment_Parent_Created");

            // Index for querying comments by account
            modelBuilder.Entity<Comment>()
                .HasIndex(c => c.AccountId)
                .HasDatabaseName("IX_Comment_AccountId");

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

            modelBuilder.Entity<Conversation>()
                .Property(c => c.Theme)
                .HasMaxLength(32);

            // Conversation → Creator (Account)
            modelBuilder.Entity<Conversation>()
                .HasOne(c => c.CreatedByAccount)
                .WithMany(a => a.CreatedConversations)
                .HasForeignKey(c => c.CreatedBy)
                .OnDelete(DeleteBehavior.Restrict);

            // Conversation → Owner (Account, group only)
            modelBuilder.Entity<Conversation>()
                .HasOne(c => c.OwnerAccount)
                .WithMany(a => a.OwnedConversations)
                .HasForeignKey(c => c.Owner)
                .OnDelete(DeleteBehavior.Restrict);

            // Data consistency: private chat must not have owner, group chat must have owner.
            modelBuilder.Entity<Conversation>()
                .ToTable(table => table.HasCheckConstraint(
                    "CK_Conversations_GroupOwner",
                    "(\"IsGroup\" = FALSE AND \"Owner\" IS NULL) OR (\"IsGroup\" = TRUE AND \"Owner\" IS NOT NULL)"));

            // Index: sort / filter conversations
            modelBuilder.Entity<Conversation>()
                .HasIndex(c => c.CreatedAt);

            modelBuilder.Entity<Conversation>()
                .HasIndex(c => c.CreatedBy);

            modelBuilder.Entity<Conversation>()
                .HasIndex(c => c.Owner);

            // Trigram index for group conversation name search (ILIKE %keyword%)
            modelBuilder.Entity<Conversation>()
                .HasIndex(c => c.ConversationName)
                .HasMethod("GIN")
                .HasOperators("gin_trgm_ops")
                .HasDatabaseName("IX_Conversations_Name_Trgm");

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

            // Index: conversation list/unread queries by account + state
            modelBuilder.Entity<ConversationMember>()
                .HasIndex(cm => new { cm.AccountId, cm.HasLeft, cm.IsMuted, cm.ConversationId })
                .HasDatabaseName("IX_ConversationMember_Account_State_Conversation");

            // Index: member list / seen-status queries by conversation + active-state
            modelBuilder.Entity<ConversationMember>()
                .HasIndex(cm => new { cm.ConversationId, cm.HasLeft, cm.AccountId })
                .HasDatabaseName("IX_ConversationMember_Conversation_HasLeft_Account");

            // Index: get members by conversation
            // Redundant with PK but good for clarity if needed, though PK (ConvId, AccId) already covers this

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

            // Index: unread/message-list predicates (conversation + sender + sent time)
            modelBuilder.Entity<Message>()
                .HasIndex(m => new { m.ConversationId, m.AccountId, m.SentAt })
                .HasDatabaseName("IX_Message_Conversation_Account_SentAt");

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

            // Message Reply (Self-Reference)
            modelBuilder.Entity<Message>()
                .HasOne(m => m.ReplyToMessage)
                .WithMany()
                .HasForeignKey(m => m.ReplyToMessageId)
                .OnDelete(DeleteBehavior.SetNull);



            // =====================
            // MESSAGE MEDIA
            // =====================
            modelBuilder.Entity<MessageMedia>()
                .HasKey(mm => mm.MessageMediaId);

            // Index: load media by message
            modelBuilder.Entity<MessageMedia>()
                .HasIndex(mm => mm.MessageId);

            // Index: media/files panel filtering by type and ordering by media created time
            modelBuilder.Entity<MessageMedia>()
                .HasIndex(mm => new { mm.MediaType, mm.CreatedAt, mm.MessageId })
                .HasDatabaseName("IX_MessageMedia_Type_Created_Message");

            modelBuilder.Entity<MessageMedia>()
                .HasOne(mm => mm.Message)
                .WithMany(m => m.Medias)
                .HasForeignKey(mm => mm.MessageId)
                .OnDelete(DeleteBehavior.Cascade);

            // =====================
            // MESSAGE HIDDEN
            // =====================
            modelBuilder.Entity<MessageHidden>()
                .HasKey(mh => new { mh.MessageId, mh.AccountId });

            // Index: get hidden messages by account
            modelBuilder.Entity<MessageHidden>()
                .HasIndex(mh => mh.AccountId);

            // MessageHidden → Message
            modelBuilder.Entity<MessageHidden>()
                .HasOne(mh => mh.Message)
                .WithMany(m => m.HiddenBy)
                .HasForeignKey(mh => mh.MessageId)
                .OnDelete(DeleteBehavior.Cascade);

            // MessageHidden → Account
            modelBuilder.Entity<MessageHidden>()
                .HasOne(mh => mh.Account)
                .WithMany()  // No navigation from Account
                .HasForeignKey(mh => mh.AccountId)
                .OnDelete(DeleteBehavior.Cascade);

            // =====================
            // MESSAGE REACT
            // =====================
            modelBuilder.Entity<MessageReact>()
                .HasKey(mr => new { mr.MessageId, mr.AccountId });

            // Composite index for reaction stats by message/type.
            // PK (MessageId, AccountId) already covers lookups by MessageId.
            modelBuilder.Entity<MessageReact>()
                .HasIndex(mr => new { mr.MessageId, mr.ReactType })
                .HasDatabaseName("IX_MessageReacts_MessageId_ReactType");

            // Index: get reactions by account
            modelBuilder.Entity<MessageReact>()
                .HasIndex(mr => mr.AccountId);

            // MessageReact → Message
            modelBuilder.Entity<MessageReact>()
                .HasOne(mr => mr.Message)
                .WithMany(m => m.Reacts)
                .HasForeignKey(mr => mr.MessageId)
                .OnDelete(DeleteBehavior.Cascade);

            // MessageReact → Account
            modelBuilder.Entity<MessageReact>()
                .HasOne(mr => mr.Account)
                .WithMany()  // No navigation from Account
                .HasForeignKey(mr => mr.AccountId)
                .OnDelete(DeleteBehavior.Cascade);

            // =====================
            // PINNED MESSAGE
            // =====================
            modelBuilder.Entity<PinnedMessage>()
                .HasKey(pm => new { pm.ConversationId, pm.MessageId });

            // Index: get pinned messages by conversation (sorted by pin time)
            modelBuilder.Entity<PinnedMessage>()
                .HasIndex(pm => new { pm.ConversationId, pm.PinnedAt })
                .HasDatabaseName("IX_PinnedMessage_Conversation_PinnedAt");

            // PinnedMessage → Conversation
            modelBuilder.Entity<PinnedMessage>()
                .HasOne(pm => pm.Conversation)
                .WithMany()  // No navigation from Conversation
                .HasForeignKey(pm => pm.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);

            // PinnedMessage → Message
            modelBuilder.Entity<PinnedMessage>()
                .HasOne(pm => pm.Message)
                .WithMany()  // No navigation from Message
                .HasForeignKey(pm => pm.MessageId)
                .OnDelete(DeleteBehavior.Cascade);

            // PinnedMessage → Account (who pinned)
            modelBuilder.Entity<PinnedMessage>()
                .HasOne(pm => pm.PinnedByAccount)
                .WithMany()  // No navigation from Account
                .HasForeignKey(pm => pm.PinnedBy)
                .OnDelete(DeleteBehavior.Restrict);
        }

    }
}
