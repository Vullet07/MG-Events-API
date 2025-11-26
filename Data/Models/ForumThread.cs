using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Models
{
    public class ForumThread
    {
        [Required]
        public int Id { get; set; }

        public required string Title { get; set; }

        public bool IsLocked { get; set; }

        public bool IsPinned { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
       
        public DateTime? LastPostAt { get; set; }

        public required User CreatedByUser { get; set; }

        public ICollection<ForumPost> Posts { get; set; } = new List<ForumPost>();
    }
}
