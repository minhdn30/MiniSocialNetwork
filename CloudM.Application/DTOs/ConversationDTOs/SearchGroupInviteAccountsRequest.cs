using System;
using System.Collections.Generic;

namespace CloudM.Application.DTOs.ConversationDTOs
{
    public class SearchGroupInviteAccountsRequest
    {
        public string? Keyword { get; set; }
        public List<Guid>? ExcludeAccountIds { get; set; }
        public int Limit { get; set; } = 10;
    }
}
