namespace CloudM.Application.DTOs.AdminDTOs
{
    public class AdminModerationItemResponse
    {
        public Guid TargetId { get; set; }
        public string TargetType { get; set; } = string.Empty;
        public Guid OwnerAccountId { get; set; }
        public string OwnerUsername { get; set; } = string.Empty;
        public string OwnerFullname { get; set; } = string.Empty;
        public string OwnerEmail { get; set; } = string.Empty;
        public string LookupLabel { get; set; } = string.Empty;
        public string PrimaryText { get; set; } = string.Empty;
        public string SecondaryText { get; set; } = string.Empty;
        public string? ContentPreview { get; set; }
        public string CurrentState { get; set; } = string.Empty;
        public bool IsRemoved { get; set; }
        public bool CanRestore { get; set; }
        public Guid? ParentCommentId { get; set; }
        public Guid? RelatedPostId { get; set; }
        public string? RelatedPostCode { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
