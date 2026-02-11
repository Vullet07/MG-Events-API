using System.ComponentModel.DataAnnotations;

namespace Data.Models
{
    public class PostVote
    {
        [Required]
        public int Id { get; set; }

        public required User User { get; set; }

        public required ForumPost Post { get; set; }

        public VoteValue Value { get; set; }
    }
}
