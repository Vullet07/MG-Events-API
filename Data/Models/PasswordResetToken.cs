using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Models
{
    public class PasswordResetToken
    {
        [Required]
        public int Id { get; set; }

        public required User User { get; set; }

        public required string TokenHash { get; set; }

        public DateTime ExpiresAt { get; set; }

        public bool IsUsed { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
