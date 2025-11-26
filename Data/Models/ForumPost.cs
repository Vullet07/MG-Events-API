using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Models
{
    public class ForumPost
    {
        [Required]
        public int Id { get; set; }

        public string? Title { get; set; }

        public required string Content { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        public bool IsDeleted { get; set; }

        public required User User { get; set; }

        public required ForumThread Thread { get; set; }

        public ICollection<ForumPost> Replies { get; set; } = new List<ForumPost>();
    }
}
