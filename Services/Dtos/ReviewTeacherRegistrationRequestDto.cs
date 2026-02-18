using System.ComponentModel.DataAnnotations;

namespace Services.Dtos
{
    public class ReviewTeacherRegistrationRequestDto
    {
        [MaxLength(300)]
        public string? Note { get; set; }
    }
}
