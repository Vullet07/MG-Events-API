using Data.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.Dtos
{
    public class UserDto
    {
        public int Id { get; set; }
        public string Username { get; set; } = default!;
        [EmailAddress]
        public string Email { get; set; } = default!;
        public Role Role { get; set; }
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
