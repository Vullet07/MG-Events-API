using Microsoft.AspNetCore.Http;

namespace WebAPI.Models
{
    public class CreateEventPinForm
    {
        public string Title { get; set; } = default!;
        public string? Description { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public IFormFile? Photo { get; set; }
    }
}
