using System;
using System.Collections.Generic;

namespace CloudM.Application.DTOs.AccountDTOs
{
    public class SearchPostTagAccountsRequest
    {
        public string? Keyword { get; set; }
        public int? Privacy { get; set; }
        public List<Guid>? ExcludeAccountIds { get; set; }
        public int Limit { get; set; } = 10;
    }
}
