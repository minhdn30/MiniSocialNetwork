using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CloudM.Application.DTOs.PostDTOs
{
    public class PostUpdateRequest
    {
        [MaxLength(3000)]
        public string? Content { get; set; }
        public int? Privacy { get; set; }
        public List<IFormFile>? NewMediaFiles { get; set; }
        public List<Guid>? RemoveMediaIds { get; set; }

    }
}
