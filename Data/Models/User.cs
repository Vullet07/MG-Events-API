using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Models
{
    public class User
    {
        [Required]
        public int Id { get; set; }

        [MaxLength(200)]
        public required string Username { get; set; }
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
    }
}
