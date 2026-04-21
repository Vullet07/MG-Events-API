using System.ComponentModel.DataAnnotations;

namespace Data.Models
{
    public class EventPinResolveConfirmation
    {
        [Required]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        public required User User { get; set; }

        [Required]
        public int PinId { get; set; }

        public required EventPin Pin { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
