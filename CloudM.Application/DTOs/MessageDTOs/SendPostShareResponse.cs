namespace CloudM.Application.DTOs.MessageDTOs
{
    public class SendPostShareResponse
    {
        public int TotalRequested { get; set; }
        public int TotalSucceeded { get; set; }
        public int TotalFailed { get; set; }
        public List<PostShareSendResult> Results { get; set; } = new();
    }

    public class PostShareSendResult
    {
        public Guid ConversationId { get; set; }
        public Guid? ReceiverId { get; set; }
        public bool IsSuccess { get; set; }
        public string? ErrorMessage { get; set; }
        public SendMessageResponse? Message { get; set; }
    }
}
