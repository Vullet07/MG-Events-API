using Data.Models;
using Services.Validators;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.Dtos
{
    public class CreateUserDto
    {
        [MaxLength(100)]
        [MinLength(4)]
        public required string Username { get; set; }
        [EmailAddress]
        public required string Email { get; set; }
        [PasswordPolicy]
        public required string Password { get; set; }

        [Range(1, 12)]
        public int GradeLevel { get; set; }

        public string? PhotoUrl { get; set; }
    }
}
