using SocialNetwork.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Infrastructure.Models
{
    public class AccountReactListModel
    {
        public Guid AccountId { get; set; }
        public string? Username { get; set; }
        public string? FullName { get; set; }
        public string? AvatarUrl { get; set; }
        public ReactEnum ReactType { get; set; }
    }
}
