using Data.Models;
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
        public string Username { get; set; }
        [EmailAddress]
        public string Email { get; set; }
        public string Password { get; set; }
        public string? PhotoUrl { get; set; }
    }
}
