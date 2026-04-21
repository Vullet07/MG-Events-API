using System.ComponentModel.DataAnnotations;

namespace Data.Models
{
    public class TeacherRegistrationRequest
    {
        [Required]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Username { get; set; } = default!;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = default!;

        [Required]
        public string PasswordHash { get; set; } = default!;

        [MaxLength(500)]
        public string? Motivation { get; set; }

        public TeacherRegistrationStatus Status { get; set; } = TeacherRegistrationStatus.Pending;

        public bool IsEmailConfirmed { get; set; }
        public string? EmailConfirmationTokenHash { get; set; }
        public DateTime? EmailConfirmationTokenExpiresAt { get; set; }
        public DateTime? EmailConfirmedAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ReviewedAt { get; set; }

        [MaxLength(300)]
        public string? ReviewNote { get; set; }

        public User? ReviewedBy { get; set; }
    }
}
