using SocialNetwork.Domain.Enums;

namespace SocialNetwork.Infrastructure.Models
{
    public class MessageReactSummaryModel
    {
        public ReactEnum ReactType { get; set; }
        public int Count { get; set; }
    }
}
