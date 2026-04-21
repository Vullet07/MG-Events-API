using Data.Models;
using Services.Validators;
using System.ComponentModel.DataAnnotations;

namespace Services.Dtos
{
    public class CreateUserDto
    {
        [MaxLength(100)]
        [MinLength(4)]
        public required string Username { get; set; }

        [EmailAddress]
        [SchoolEmail]
        public required string Email { get; set; }

        [PasswordPolicy]
        public required string Password { get; set; }

        [Range(1, 12)]
        public int GradeLevel { get; set; }

        public string? PhotoUrl { get; set; }
    }
}
