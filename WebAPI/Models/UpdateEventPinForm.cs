using Microsoft.AspNetCore.Http;

namespace WebAPI.Models
{
    public class UpdateEventPinForm
    {
        public string Title { get; set; } = default!;
        public string? Description { get; set; }
        public string Category { get; set; } = default!;
        public IFormFile? Photo { get; set; }
        public bool RemovePhoto { get; set; }
    }
}
