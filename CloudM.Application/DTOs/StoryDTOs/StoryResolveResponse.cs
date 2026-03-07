namespace CloudM.Application.DTOs.StoryDTOs
{
    public class StoryResolveResponse
    {
        public Guid StoryId { get; set; }
        public Guid AuthorId { get; set; }
        public string StoryMode { get; set; } = "active";
        public int? ArchivePage { get; set; }
        public int? ArchivePageSize { get; set; }
    }
}
