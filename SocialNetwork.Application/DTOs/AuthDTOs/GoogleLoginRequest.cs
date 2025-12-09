using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Application.DTOs.AuthDTOs
{
    public class GoogleLoginRequest
    {
        public string IdToken { get; set; } = null!;
    }
}
