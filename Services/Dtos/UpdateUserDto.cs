using Data.Models;
using Services.Validators;
using System.ComponentModel.DataAnnotations;

namespace Services.Dtos
{
    public class UpdateUserDto
    {
        [MaxLength(100)]
        [MinLength(4)]
        [UsernamePolicy]
        public string? Username { get; set; }

        [EmailAddress]
        [SchoolEmail]
        public string? Email { get; set; }

        public string? Password { get; set; }
        public Role? Role { get; set; }
        public string? PhotoUrl { get; set; }
    }
}
