namespace Services.Dtos
{
    public class PublicUserThreadItemDto
    {
        public int ThreadId { get; set; }
        public string Title { get; set; } = default!;
        public DateTime CreatedAt { get; set; }
        public DateTime? LastPostAt { get; set; }
        public bool IsPinned { get; set; }
        public bool IsLocked { get; set; }
    }

    public class PublicUserPostItemDto
    {
        public int PostId { get; set; }
        public int ThreadId { get; set; }
        public string ThreadTitle { get; set; } = default!;
        public string? Title { get; set; }
        public string Content { get; set; } = default!;
        public string? PhotoUrl { get; set; }
        public DateTime CreatedAt { get; set; }
        public int? ParentPostId { get; set; }
        public int Upvotes { get; set; }
        public int Downvotes { get; set; }
        public int Score => Upvotes - Downvotes;
    }

    public class PublicUserPinItemDto
    {
        public int PinId { get; set; }
        public string Title { get; set; } = default!;
        public string? Description { get; set; }
        public string? PhotoUrl { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public DateTime CreatedAt { get; set; }
        public int Upvotes { get; set; }
        public int Downvotes { get; set; }
        public int Score => Upvotes - Downvotes;
    }
}
