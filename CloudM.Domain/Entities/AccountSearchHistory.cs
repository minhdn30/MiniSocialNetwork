namespace CloudM.Domain.Entities
{
    public class AccountSearchHistory
    {
        public Guid CurrentId { get; set; }
        public Guid TargetId { get; set; }
        public DateTime LastSearchedAt { get; set; } = DateTime.UtcNow;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual Account Current { get; set; } = null!;
        public virtual Account Target { get; set; } = null!;
    }
}
