using System.ComponentModel.DataAnnotations;

namespace Data.Models
{
    public class UserActionQuotaEvent
    {
        [Required]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        public required User User { get; set; }

        [Required]
        public UserActionQuotaType ActionType { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
