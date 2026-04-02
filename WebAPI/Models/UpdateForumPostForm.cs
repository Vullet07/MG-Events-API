using Microsoft.AspNetCore.Http;

namespace WebAPI.Models
{
    public class UpdateForumPostForm
    {
        public string? Title { get; set; }
        public string? Content { get; set; }
        public IFormFile? Photo { get; set; }
        public bool RemovePhoto { get; set; }
    }
}
