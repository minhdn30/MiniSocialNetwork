namespace CloudM.Application.DTOs.BlockDTOs
{
    public class BlockStatusResponse
    {
        public Guid TargetId { get; set; }
        public bool IsBlockedByCurrentUser { get; set; }
        public bool IsBlockedByTargetUser { get; set; }
        public bool IsBlockedEitherWay { get; set; }
    }
}
