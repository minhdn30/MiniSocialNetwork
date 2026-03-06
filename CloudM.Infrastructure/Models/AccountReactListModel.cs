using CloudM.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CloudM.Infrastructure.Models
{
    public class AccountReactListModel
    {
        public Guid AccountId { get; set; }
        public string? Username { get; set; }
        public string? FullName { get; set; }
        public string? AvatarUrl { get; set; }
        public ReactEnum ReactType { get; set; }
        public bool IsFollowing { get; set; }
        public bool IsFollower { get; set; }
    }
}
