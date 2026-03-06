using System;
using System.Collections.Generic;

namespace CloudM.Application.DTOs.MessageDTOs
{
    public class SearchPostShareTargetsResponse
    {
        public string Keyword { get; set; } = string.Empty;
        public int Limit { get; set; }
        public int Total { get; set; }
        public List<PostShareTargetSearchItemResponse> Items { get; set; } = new();
    }

    public class PostShareTargetSearchItemResponse
    {
        public string TargetType { get; set; } = "user";
        public Guid? AccountId { get; set; }
        public Guid? ConversationId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Subtitle { get; set; }
        public string? AvatarUrl { get; set; }
        public List<string>? GroupAvatars { get; set; }
        public bool UseGroupIcon { get; set; }
        public bool IsContacted { get; set; }
        public DateTime? LastContactedAt { get; set; }
        public double MatchScore { get; set; }
    }
}
