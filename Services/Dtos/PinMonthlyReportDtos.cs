namespace Services.Dtos
{
    public class PinMonthlyReportDto
    {
        public string SchoolName { get; set; } = "МГ \"Академик Кирил Попов\" - Пловдив";
        public string MonthKey { get; set; } = default!;
        public string MonthLabel { get; set; } = default!;
        public DateTime GeneratedAt { get; set; }
        public int TotalPins { get; set; }
        public int PinsWithPhotos { get; set; }
        public int ActiveZones { get; set; }
        public List<PinHotspotDto> Hotspots { get; set; } = [];
        public List<PinCategoryStatDto> Categories { get; set; } = [];
        public List<PinReportItemDto> TopPins { get; set; } = [];
    }

    public class PinHotspotDto
    {
        public string LayerLabel { get; set; } = default!;
        public string ZoneLabel { get; set; } = default!;
        public string ZoneKind { get; set; } = default!;
        public int PinsCount { get; set; }
        public int TotalScore { get; set; }
        public int HighestScore { get; set; }
        public string DominantCategory { get; set; } = default!;
        public DateTime LatestPinAt { get; set; }
    }

    public class PinCategoryStatDto
    {
        public string Category { get; set; } = default!;
        public int PinsCount { get; set; }
        public int TotalScore { get; set; }
    }

    public class PinReportItemDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = default!;
        public string Category { get; set; } = default!;
        public string LayerLabel { get; set; } = default!;
        public string ZoneLabel { get; set; } = default!;
        public string CreatedByUsername { get; set; } = default!;
        public int Score { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
