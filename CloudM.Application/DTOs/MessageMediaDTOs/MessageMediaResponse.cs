using CloudM.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CloudM.Application.DTOs.MessageMediaDTOs
{
    public class MessageMediaResponse
    {
        public Guid MessageMediaId { get; set; }
        public string MediaUrl { get; set; } = null!;
        public string? ThumbnailUrl { get; set; }
        public MediaTypeEnum MediaType { get; set; }
        public string? FileName { get; set; } // hiển thị & download
        public long? FileSize { get; set; } // byte
        public DateTime CreatedAt { get; set; }
    }
}
