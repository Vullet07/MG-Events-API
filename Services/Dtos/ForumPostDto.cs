using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.Dtos
{
    public class ForumPostDto
    {
        public int Id { get; set; }
        public string? Title { get; set; }
        public string? PhotoUrl { get; set; }
        public string Content { get; set; } = default!;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int UserId { get; set; }
        public int ThreadId { get; set; }
    }
}
