namespace CloudM.Application.DTOs.MessageDTOs
{
    public class RecallMessageResponse
    {
        public Guid MessageId { get; set; }
        public Guid ConversationId { get; set; }
        public DateTime RecalledAt { get; set; }
    }
}
