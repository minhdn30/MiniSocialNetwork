using CloudM.Domain.Enums;

namespace CloudM.Infrastructure.Models
{
    public class MessageReactSummaryModel
    {
        public ReactEnum ReactType { get; set; }
        public int Count { get; set; }
    }
}
