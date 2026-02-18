using Services.Validators;
using System.ComponentModel.DataAnnotations;

namespace Services.Dtos
{
    public class CreateTeacherRegistrationRequestDto
    {
        [MaxLength(100)]
        [MinLength(4)]
        public required string Username { get; set; }

        [EmailAddress]
        public required string Email { get; set; }

        [PasswordPolicy]
        public required string Password { get; set; }

        [MaxLength(500)]
        public string? Motivation { get; set; }
    }
}
