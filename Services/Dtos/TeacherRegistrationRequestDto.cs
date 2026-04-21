using Data.Models;

namespace Services.Dtos
{
    public class TeacherRegistrationRequestDto
    {
        public int Id { get; set; }
        public string Username { get; set; } = default!;
        public string Email { get; set; } = default!;
        public bool IsEmailConfirmed { get; set; }
        public DateTime? EmailConfirmedAt { get; set; }
        public string? Motivation { get; set; }
        public TeacherRegistrationStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public string? ReviewNote { get; set; }
        public string? ReviewedByUsername { get; set; }
    }
}
