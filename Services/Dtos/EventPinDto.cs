namespace Services.Dtos
{
    public class EventPinDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = default!;
        public string? Description { get; set; }
        public string Category { get; set; } = default!;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string? PhotoUrl { get; set; }
        public DateTime CreatedAt { get; set; }
        public int CreatedByUserId { get; set; }
        public string CreatedByUsername { get; set; } = default!;
        public bool IsResolved { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public int? ResolvedByUserId { get; set; }
        public string? ResolvedByUsername { get; set; }
        public DateTime? ArchivedAt { get; set; }
        public int ResolveConfirmationCount { get; set; }
        public int ResolveThreshold { get; set; }
        public bool HasCurrentUserResolveConfirmation { get; set; }
        public string LayerId { get; set; } = default!;
        public string LayerLabel { get; set; } = default!;
        public string ZoneId { get; set; } = default!;
        public string ZoneLabel { get; set; } = default!;
        public string ZoneKind { get; set; } = default!;
        public int Upvotes { get; set; }
        public int Downvotes { get; set; }
        public int Score { get; set; }
        public int MyVote { get; set; }
    }
}
