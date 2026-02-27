using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CloudM.Application.DTOs.FollowDTOs
{
    public class FollowPagingRequest
    {
        public string? Keyword { get; set; }
        public bool? SortByCreatedASC { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }
}
