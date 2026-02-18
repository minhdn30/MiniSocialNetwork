using System.Collections.Generic;

namespace SocialNetwork.Application.DTOs.CommonDTOs
{
    public class CursorResponse<T>
    {
        public IEnumerable<T> Items { get; set; } = new List<T>();
        public string? OlderCursor { get; set; }
        public string? NewerCursor { get; set; }
        public bool HasMoreOlder { get; set; }
        public bool HasMoreNewer { get; set; }

        public CursorResponse() { }

        public CursorResponse(IEnumerable<T> items, string? olderCursor, string? newerCursor, bool hasMoreOlder, bool hasMoreNewer)
        {
            Items = items;
            OlderCursor = olderCursor;
            NewerCursor = newerCursor;
            HasMoreOlder = hasMoreOlder;
            HasMoreNewer = hasMoreNewer;
        }
    }
}
