namespace CloudM.Application.DTOs.MessageDTOs
{
    public class SendPostShareRequest
    {
        public Guid PostId { get; set; }
        public List<Guid>? ConversationIds { get; set; }
        public List<Guid>? ReceiverIds { get; set; }
        public string? Content { get; set; }
        public string? TempId { get; set; }
    }
}
