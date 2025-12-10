using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Application.DTOs.FollowDTOs
{
    public class FollowCountResponse
    {
        public int Followers { get; set; }
        public int Following { get; set; }
        public bool IsFollowedByCurrentUser { get; set; } = false;
    }
}
