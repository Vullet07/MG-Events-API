using Data.Models;
using System.ComponentModel.DataAnnotations;

namespace Services.Dtos
{
    public class CreateReportDto
    {
        public ReportTargetType TargetType { get; set; }
        public int TargetId { get; set; }
        [MaxLength(200)]
        public string Reason { get; set; } = default!;
        [MaxLength(2000)]
        public string? Details { get; set; }
    }
}
