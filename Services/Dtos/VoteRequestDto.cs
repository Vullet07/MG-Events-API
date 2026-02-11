using System.ComponentModel.DataAnnotations;

namespace Services.Dtos
{
    public class VoteRequestDto
    {
        [Range(-1, 1)]
        public int Value { get; set; }
    }
}
