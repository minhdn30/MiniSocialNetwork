using System;
using System.Collections.Generic;

namespace CloudM.Application.DTOs.PostDTOs
{
    public class PostTagSummaryResponse
    {
        public Guid PostId { get; set; }
        public List<PostTaggedAccountResponse> TaggedAccountsPreview { get; set; } = new();
        public int TotalTaggedAccounts { get; set; }
        public bool IsCurrentUserTagged { get; set; }
    }
}
