using Microsoft.AspNetCore.Http;

namespace WebAPI.Models
{
    public class CreateForumPostForm
    {
        public string? Title { get; set; }
        public string Content { get; set; } = default!;
        public int ThreadId { get; set; }
        public int? ParentPostId { get; set; }
        public IFormFile? Photo { get; set; }
    }
}
