using SocialNetwork.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Application.DTOs.PostMediaDTOs
{
    public class PostMediaProfilePreviewModel
    {
        public Guid MediaId { get; set; }
        public Guid PostId { get; set; }
        public string? PostCode { get; set; }
        public string MediaUrl { get; set; } = null!;
        public MediaTypeEnum MediaType { get; set; }
    }
}
