using System.ComponentModel.DataAnnotations;

namespace Data.Models
{
    public class Report
    {
        [Required]
        public int Id { get; set; }

        public required User Reporter { get; set; }

        public ReportTargetType TargetType { get; set; }

        public int TargetId { get; set; }

        [MaxLength(200)]
        public required string Reason { get; set; }

        [MaxLength(2000)]
        public string? Details { get; set; }

        public ReportStatus Status { get; set; } = ReportStatus.Open;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? ResolvedAt { get; set; }

        public User? ResolvedBy { get; set; }
    }
}
