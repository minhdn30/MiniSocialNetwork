namespace CloudM.Application.DTOs.MessageDTOs
{
    public class ForwardMessageRequest
    {
        public Guid SourceMessageId { get; set; }
        public List<Guid>? ConversationIds { get; set; }
        public List<Guid>? ReceiverIds { get; set; }
        public string? TempId { get; set; }
    }
}
