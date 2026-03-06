using Microsoft.AspNetCore.Http;
using CloudM.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CloudM.Application.DTOs.PostMediaDTOs
{
    public class PostMediaCreateRequest
    {
        public IFormFile File { get; set; } = null!;
        public MediaTypeEnum Type { get; set; }
    }
}
