using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Application.DTOs.PostMediaDTOs
{
    public class PostMediaCropInfoRequest
    {
        public int Index { get; set; }
        public float? CropX { get; set; }
        public float? CropY { get; set; }
        public float? CropWidth { get; set; }
        public float? CropHeight { get; set; }
    }
}
