using System.ComponentModel.DataAnnotations;

namespace Data.Models
{
    public class User
    {
        [Required]
        public int Id { get; set; }

        [MaxLength(200)]
        public required string Username { get; set; }

        [EmailAddress]
        public required string Email { get; set; }

        [Required]
        public string? PasswordHash { get; set; }

        [Required]
        public Role Role { get; set; }

        public string? PhotoUrl { get; set; }

        public bool IsDeleted { get; set; }

        public DateTime? DeletedAt { get; set; }

        public bool IsBanned { get; set; }
        public DateTime? BannedUntil { get; set; }
        public string? BanReason { get; set; }

        public bool IsEmailConfirmed { get; set; }
        public string? EmailConfirmationTokenHash { get; set; }
        public DateTime? EmailConfirmationTokenExpiresAt { get; set; }
        public DateTime? EmailConfirmedAt { get; set; }

        [Range(1, 12)]
        public int? GradeLevel { get; set; }

        public int? SchoolYearStart { get; set; }

        public DateTime? ScheduledDeletionAt { get; set; }
    }
}
