using System.ComponentModel.DataAnnotations;

namespace Services.Dtos
{
    public class ConfirmEmailDto
    {
        [Required]
        public string Token { get; set; } = default!;

        [Required]
        public string Kind { get; set; } = default!;
    }
}
