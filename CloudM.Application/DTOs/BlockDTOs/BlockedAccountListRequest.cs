namespace CloudM.Application.DTOs.BlockDTOs
{
    public class BlockedAccountListRequest
    {
        private const int DefaultPageSize = 20;
        private const int MaxPageSize = 50;

        public string? Keyword { get; set; }
        public int Page { get; set; } = 1;

        private int _pageSize = DefaultPageSize;
        public int PageSize
        {
            get => _pageSize;
            set => _pageSize = value <= 0 ? DefaultPageSize : Math.Min(value, MaxPageSize);
        }
    }
}
