using Data;
using Data.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Services.Seeding
{
    public class DataSeeder : IDataSeeder
    {
        private const int TargetCount = 50;

        private readonly AppDbContext _db;
        private readonly IPasswordHasher<User> _passwordHasher;

        public DataSeeder(AppDbContext db, IPasswordHasher<User> passwordHasher)
        {
            _db = db;
            _passwordHasher = passwordHasher;
        }

        public async Task SeedAsync()
        {
            await ClearAllAsync();

            var users = CreateUsers();
            _db.Users.AddRange(users);
            await _db.SaveChangesAsync();

            var admin = users.First(u => u.Role == Role.Admin);
            var teacher = users.First(u => u.Role == Role.Teacher);
            var students = users.Where(u => u.Role == Role.Student).ToList();

            var threads = CreateThreads(users);
            _db.ForumThreads.AddRange(threads);
            await _db.SaveChangesAsync();

            var posts = CreatePosts(users, threads);
            _db.ForumPosts.AddRange(posts);
            await _db.SaveChangesAsync();

            foreach (var thread in threads)
            {
                var latest = posts.Where(p => p.Thread.Id == thread.Id).OrderByDescending(p => p.CreatedAt).FirstOrDefault();
                thread.LastPostAt = latest?.CreatedAt;
            }
            await _db.SaveChangesAsync();

            var pins = CreatePins(users);
            _db.EventPins.AddRange(pins);
            await _db.SaveChangesAsync();

            var postVotes = CreatePostVotes(users, posts);
            _db.PostVotes.AddRange(postVotes);

            var pinVotes = CreatePinVotes(users, pins);
            _db.PinVotes.AddRange(pinVotes);

            var reports = CreateReports(users, posts, threads, pins);
            _db.Reports.AddRange(reports);

            var passwordTokens = CreatePasswordResetTokens(users);
            _db.PasswordResetTokens.AddRange(passwordTokens);

            var teacherRequests = CreateTeacherRegistrationRequests(admin, teacher);
            _db.TeacherRegistrationRequests.AddRange(teacherRequests);

            await _db.SaveChangesAsync();
        }

        private async Task ClearAllAsync()
        {
            _db.PostVotes.RemoveRange(_db.PostVotes);
            _db.PinVotes.RemoveRange(_db.PinVotes);
            _db.Reports.RemoveRange(_db.Reports);
            _db.PasswordResetTokens.RemoveRange(_db.PasswordResetTokens);
            _db.ForumPosts.RemoveRange(_db.ForumPosts);
            _db.ForumThreads.RemoveRange(_db.ForumThreads);
            _db.EventPins.RemoveRange(_db.EventPins);
            _db.TeacherRegistrationRequests.RemoveRange(_db.TeacherRegistrationRequests);
            _db.Users.RemoveRange(_db.Users.IgnoreQueryFilters());
            await _db.SaveChangesAsync();
        }

        private List<User> CreateUsers()
        {
            var users = new List<User>(TargetCount)
            {
                CreateUser("admin", "admin@mg-events.com", Role.Admin),
                CreateUser("teacher", "teacher@mg-events.com", Role.Teacher)
            };

            for (var i = 1; i <= TargetCount - 2; i++)
            {
                users.Add(CreateUser($"student{i:00}", $"student{i:00}@mg-events.com", Role.Student));
            }

            return users;
        }

        private List<ForumThread> CreateThreads(List<User> users)
        {
            var threads = new List<ForumThread>(TargetCount);
            for (var i = 1; i <= TargetCount; i++)
            {
                var creator = users[(i - 1) % users.Count];
                threads.Add(new ForumThread
                {
                    Title = i % 10 == 0 ? $"[News] Campus update {i}" : $"Community thread {i}",
                    CreatedByUser = creator,
                    CreatedAt = DateTime.UtcNow.AddDays(-i),
                    IsPinned = i <= 3,
                    IsLocked = i % 13 == 0
                });
            }

            return threads;
        }

        private List<ForumPost> CreatePosts(List<User> users, List<ForumThread> threads)
        {
            var posts = new List<ForumPost>(TargetCount);

            for (var i = 1; i <= TargetCount; i++)
            {
                var thread = threads[(i - 1) % threads.Count];
                var author = users[(i * 3) % users.Count];
                var post = new ForumPost
                {
                    Title = i % 4 == 0 ? null : $"Post {i} in {thread.Title}",
                    Content = $"Detailed report content for post #{i}. Includes practical context and proposed actions.",
                    Thread = thread,
                    User = author,
                    CreatedAt = DateTime.UtcNow.AddHours(-i * 3),
                    IsDeleted = false
                };

                if (i > 10 && i % 5 == 0)
                {
                    var parent = posts[(i - 7) % posts.Count];
                    post.ParentPost = parent;
                    post.ParentPostId = parent.Id;
                }

                posts.Add(post);
            }

            return posts;
        }

        private List<EventPin> CreatePins(List<User> users)
        {
            var pins = new List<EventPin>(TargetCount);
            var baseLat = 42.6977;
            var baseLng = 23.3219;

            for (var i = 1; i <= TargetCount; i++)
            {
                pins.Add(new EventPin
                {
                    Title = $"Event pin {i}",
                    Description = $"Observed issue #{i} with context for maintenance teams.",
                    Latitude = baseLat + (i % 10) * 0.0012,
                    Longitude = baseLng + (i % 10) * 0.0011,
                    CreatedByUser = users[(i * 5) % users.Count],
                    CreatedAt = DateTime.UtcNow.AddHours(-i * 2)
                });
            }

            return pins;
        }

        private List<PostVote> CreatePostVotes(List<User> users, List<ForumPost> posts)
        {
            var votes = new List<PostVote>(TargetCount);
            var used = new HashSet<string>();
            var idx = 0;

            while (votes.Count < TargetCount)
            {
                var user = users[idx % users.Count];
                var post = posts[(idx * 7) % posts.Count];
                var key = $"{user.Id}:{post.Id}";
                if (!used.Contains(key))
                {
                    used.Add(key);
                    votes.Add(new PostVote
                    {
                        User = user,
                        Post = post,
                        Value = idx % 3 == 0 ? VoteValue.Down : VoteValue.Up
                    });
                }
                idx++;
            }

            return votes;
        }

        private List<PinVote> CreatePinVotes(List<User> users, List<EventPin> pins)
        {
            var votes = new List<PinVote>(TargetCount);
            var used = new HashSet<string>();
            var idx = 0;

            while (votes.Count < TargetCount)
            {
                var user = users[(idx * 2) % users.Count];
                var pin = pins[(idx * 9) % pins.Count];
                var key = $"{user.Id}:{pin.Id}";
                if (!used.Contains(key))
                {
                    used.Add(key);
                    votes.Add(new PinVote
                    {
                        User = user,
                        Pin = pin,
                        Value = idx % 4 == 0 ? VoteValue.Down : VoteValue.Up
                    });
                }
                idx++;
            }

            return votes;
        }

        private List<Report> CreateReports(List<User> users, List<ForumPost> posts, List<ForumThread> threads, List<EventPin> pins)
        {
            var reports = new List<Report>(TargetCount);
            for (var i = 1; i <= TargetCount; i++)
            {
                var reporter = users[(i * 11) % users.Count];
                var type = (ReportTargetType)((i - 1) % 4);
                var targetId = type switch
                {
                    ReportTargetType.Post => posts[(i * 3) % posts.Count].Id,
                    ReportTargetType.Thread => threads[(i * 5) % threads.Count].Id,
                    ReportTargetType.Pin => pins[(i * 7) % pins.Count].Id,
                    _ => users[(i * 13) % users.Count].Id
                };

                reports.Add(new Report
                {
                    Reporter = reporter,
                    TargetType = type,
                    TargetId = targetId,
                    Reason = $"Automated moderation seed reason #{i}",
                    Details = $"Moderation context #{i}",
                    Status = i % 5 == 0 ? ReportStatus.Reviewed : ReportStatus.Open,
                    CreatedAt = DateTime.UtcNow.AddHours(-i)
                });
            }

            return reports;
        }

        private List<PasswordResetToken> CreatePasswordResetTokens(List<User> users)
        {
            var tokens = new List<PasswordResetToken>(TargetCount);
            for (var i = 1; i <= TargetCount; i++)
            {
                tokens.Add(new PasswordResetToken
                {
                    User = users[(i * 17) % users.Count],
                    TokenHash = Guid.NewGuid().ToString("N") + i,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(20 + i),
                    IsUsed = i % 3 == 0,
                    CreatedAt = DateTime.UtcNow.AddMinutes(-i)
                });
            }

            return tokens;
        }

        private List<TeacherRegistrationRequest> CreateTeacherRegistrationRequests(User admin, User teacher)
        {
            var requests = new List<TeacherRegistrationRequest>(TargetCount);
            for (var i = 1; i <= TargetCount; i++)
            {
                var status = i % 3 == 0
                    ? TeacherRegistrationStatus.Approved
                    : i % 4 == 0
                        ? TeacherRegistrationStatus.Rejected
                        : TeacherRegistrationStatus.Pending;

                requests.Add(new TeacherRegistrationRequest
                {
                    Username = $"teacher-candidate-{i:00}",
                    Email = $"teacher-candidate-{i:00}@mg-events.com",
                    PasswordHash = Guid.NewGuid().ToString("N"),
                    Motivation = $"Request #{i} to support moderation and teaching workflows.",
                    Status = status,
                    CreatedAt = DateTime.UtcNow.AddDays(-i),
                    ReviewedAt = status == TeacherRegistrationStatus.Pending ? null : DateTime.UtcNow.AddDays(-(i - 1)),
                    ReviewNote = status == TeacherRegistrationStatus.Pending ? null : (status == TeacherRegistrationStatus.Approved ? "Approved in seed." : "Rejected in seed."),
                    ReviewedBy = status == TeacherRegistrationStatus.Pending ? null : (i % 2 == 0 ? admin : teacher)
                });
            }

            return requests;
        }

        private User CreateUser(string username, string email, Role role)
        {
            var user = new User
            {
                Username = username,
                Email = email,
                Role = role,
                IsDeleted = false,
                IsBanned = false,
                PhotoUrl = null
            };

            user.PasswordHash = _passwordHasher.HashPassword(user, "Test123!");
            return user;
        }
    }
}
