using System;
using System.ComponentModel.DataAnnotations;

namespace Data.Models
{
    public class EventPin
    {
        [Required]
        public int Id { get; set; }

        [Required]
        public string Title { get; set; } = default!;

        public string? Description { get; set; }

        [Required]
        [MaxLength(80)]
        public string Category { get; set; } = default!;

        public double Latitude { get; set; }

        public double Longitude { get; set; }

        public string? PhotoUrl { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public required User CreatedByUser { get; set; }
    }
}
