using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Application.DTOs.PostReactDTOs
{
    public class ReactToggleResponse
    {
        public int ReactCount { get; set; }
        public bool IsReactedByCurrentUser { get; set; }
    }
}
