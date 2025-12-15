using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.Dtos
{
    public class CreateForumPostDto
    {
        public string? Title { get; set; }
        public string Content { get; set; } = default!;
        public int ThreadId { get; set; }
        public int? ParentPostId { get; set; }
    }
}
