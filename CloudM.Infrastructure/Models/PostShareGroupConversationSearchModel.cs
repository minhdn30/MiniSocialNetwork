using System;
using System.Collections.Generic;

namespace CloudM.Infrastructure.Models
{
    public class PostShareGroupConversationSearchModel
    {
        public Guid ConversationId { get; set; }
        public string ConversationName { get; set; } = null!;
        public string? ConversationAvatar { get; set; }
        public List<string>? GroupAvatars { get; set; }
        public bool IsContacted { get; set; }
        public DateTime? LastContactedAt { get; set; }
        public double MatchScore { get; set; }
    }
}
