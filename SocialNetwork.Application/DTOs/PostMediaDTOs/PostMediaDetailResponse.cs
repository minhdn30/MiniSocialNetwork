using SocialNetwork.Domain.Entities;
using SocialNetwork.Domain.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Application.DTOs.PostMediaDTOs
{
    public class PostMediaDetailResponse
    {
        public Guid MediaId { get; set; }
        public string MediaUrl { get; set; } = null!;
        public MediaTypeEnum Type { get; set; }
        public float? CropX { get; set; }  
        public float? CropY { get; set; }   
        public float? CropWidth { get; set; }
        public float? CropHeight { get; set; }
        public DateTime CreatedAt { get; set; }

    }
}
