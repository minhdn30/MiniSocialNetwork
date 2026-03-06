namespace CloudM.Infrastructure.Models
{
    public class PostShareInfoModel
    {
        public Guid PostId { get; set; }
        public string PostCode { get; set; } = string.Empty;
        public bool IsPostUnavailable { get; set; }
        public Guid OwnerId { get; set; }
        public string? OwnerUsername { get; set; }
        public string? OwnerDisplayName { get; set; }
        public string? ThumbnailUrl { get; set; }
        public int? ThumbnailMediaType { get; set; }
        public string? ContentSnippet { get; set; }
    }
}
