using Data.Models;

namespace Services.Dtos
{
    public class ReportDto
    {
        public int Id { get; set; }
        public ReportTargetType TargetType { get; set; }
        public int TargetId { get; set; }
        public string TargetLabel { get; set; } = default!;
        public string Reason { get; set; } = default!;
        public string? Details { get; set; }
        public ReportStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public int ReporterId { get; set; }
        public string ReporterUsername { get; set; } = default!;
        public DateTime? ResolvedAt { get; set; }
        public int? ResolvedByUserId { get; set; }
    }
}
