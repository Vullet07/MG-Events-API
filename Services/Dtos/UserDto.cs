using Data.Models;
using System.ComponentModel.DataAnnotations;

namespace Services.Dtos
{
    public class UserDto
    {
        public int Id { get; set; }
        public string Username { get; set; } = default!;

        [EmailAddress]
        public string Email { get; set; } = default!;

        public Role Role { get; set; }
        public bool IsEmailConfirmed { get; set; }
        public string? PhotoUrl { get; set; }
        public bool IsBanned { get; set; }
        public DateTime? BannedUntil { get; set; }
        public int? GradeLevel { get; set; }
        public int? SchoolYearStart { get; set; }
        public DateTime? ScheduledDeletionAt { get; set; }
        public int ThreadsCount { get; set; }
        public int PostsCount { get; set; }
        public int PinsCount { get; set; }
    }
}
