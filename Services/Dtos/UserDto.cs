using Data.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.Dtos
{
    public class UserDto
    {
        public int Id { get; set; }
        public string Username { get; set; } = default!;
        public Role Role { get; set; }
        public string? PhotoUrl { get; set; }
    }
}
