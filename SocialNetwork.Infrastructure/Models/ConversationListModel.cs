using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Infrastructure.Models
{
    public class ConversationListModel
    {
        public Guid ConversationId { get; set; }
        public string? ConversationName { get; set; }
        public MessageBasicModel? LastMessage { get; set; }
        public int UnreadMessageCount { get; set; }

    }
}
