using Data.Models;
using Services.Validators;
using System.ComponentModel.DataAnnotations;

namespace Services.Dtos
{
    public class CreateTeacherRegistrationRequestDto
    {
        [Required]
        [MaxLength(100)]
        [MinLength(4)]
        [UsernamePolicy]
        public required string Username { get; set; }

        [Required]
        [EmailAddress]
        [SchoolEmail]
        public required string Email { get; set; }

        [Required]
        [PasswordPolicy]
        public required string Password { get; set; }

        [MaxLength(500)]
        public string? Motivation { get; set; }
    }
}
