using System;
using System.Collections.Generic;

namespace CloudM.Application.DTOs.CommentDTOs
{
    public class CommentCursorResponse
    {
        public List<CommentResponse> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public CommentNextCursorResponse? NextCursor { get; set; }
    }

    public class CommentNextCursorResponse
    {
        public DateTime CreatedAt { get; set; }
        public Guid CommentId { get; set; }
    }
}
