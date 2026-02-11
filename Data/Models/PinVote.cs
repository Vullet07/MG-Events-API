using System.ComponentModel.DataAnnotations;

namespace Data.Models
{
    public class PinVote
    {
        [Required]
        public int Id { get; set; }

        public required User User { get; set; }

        public required EventPin Pin { get; set; }

        public VoteValue Value { get; set; }
    }
}
