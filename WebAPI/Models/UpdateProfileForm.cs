using System.ComponentModel.DataAnnotations;
using Services.Validators;

namespace WebAPI.Models
{
    public class UpdateProfileForm
    {
        [MaxLength(100)]
        [MinLength(4)]
        [UsernamePolicy]
        public string? Username { get; set; }

        public IFormFile? File { get; set; }
    }
}
