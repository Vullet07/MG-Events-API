using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.Dtos
{
    public class ForumThreadDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = default!;
        public bool IsLocked { get; set; }
        public bool IsPinned { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastPostAt { get; set; }
        public int CreatedByUserId { get; set; }
    }
}
