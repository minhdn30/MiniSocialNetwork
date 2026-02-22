using SocialNetwork.Domain.Enums;

namespace SocialNetwork.Domain.Entities
{
    public class StoryView
    {
        public Guid StoryId { get; set; }
        public Guid ViewerAccountId { get; set; }
        public DateTime ViewedAt { get; set; } = DateTime.UtcNow;
        public ReactEnum? ReactType { get; set; }
        public DateTime? ReactedAt { get; set; }

        public virtual Story Story { get; set; } = null!;
        public virtual Account ViewerAccount { get; set; } = null!;
    }
}
