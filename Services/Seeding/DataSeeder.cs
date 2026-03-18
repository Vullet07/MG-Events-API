using Data;
using Data.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Services.Maps;

namespace Services.Seeding
{
    public class DataSeeder : IDataSeeder
    {
        private const int TargetCount = 50;

        private static readonly (string LayerId, double X, double Y, string Title, string Description)[] PinSeeds =
        [
            ("campus", 410, 210, "Натоварен вход", "Събиране на хора пред северната алея и нужда от организация."),
            ("campus", 655, 255, "Повреда до игрището", "Зона около игрището има нужда от оглед и реакция."),
            ("campus", 690, 600, "Паркинг сигнал", "Необходим е преглед на маркировката и достъпа около паркинга."),
            ("main:1", 342, 292, "Лоби сигнал", "Нужно е внимание в лобито на голямата сграда."),
            ("main:1", 404, 205, "ФВС салон 1", "Сигнал, свързан със спортната зона на първи етаж."),
            ("main:1", 420, 655, "Столова", "Съобщение за нужда от поддръжка около столовата."),
            ("main:2", 424, 178, "ФВС салон 2", "Сигнал за оборудване в спортния салон."),
            ("main:2", 348, 318, "Коридор 2 етаж", "Необходим оглед на коридора и движението между стаите."),
            ("main:2", 190, 676, "Стълбище 2 етаж", "Проблем около стълбищната клетка."),
            ("main:3", 124, 160, "Библиотека", "Сигнал, свързан с библиотеката."),
            ("main:3", 222, 652, "Стълбище 3 етаж", "Необходим е оглед около стълбището."),
            ("main:4", 124, 160, "Лаборатория програмиране", "Проблем в лабораторията по програмиране."),
            ("main:4", 222, 652, "Стълбище 4 етаж", "Сигнал за достъп и безопасност около стълбището."),
            ("small:1", 315, 170, "Лаборатория физика", "Сигнал в зоната на физиката."),
            ("small:1", 535, 170, "Стая 113", "Проблем, отбелязан около стая 113."),
            ("small:1", 535, 498, "Стълбище малка сграда", "Сигнал в общата зона на стълбището."),
            ("small:2", 314, 170, "Стая 211", "Нужно е внимание около стая 211."),
            ("small:2", 535, 170, "Стая 213", "Сигнал, свързан със стая 213."),
            ("small:3", 315, 180, "Технологии и иновации", "Сигнал в STEM зоната."),
            ("small:3", 425, 160, "Стая 315", "Отбелязан е проблем в стая 315.")
        ];

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
                var gradeLevel = ((i - 1) % 12) + 1;
                users.Add(CreateUser($"student{i:00}", $"student{i:00}@mg-events.com", Role.Student, gradeLevel));
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

            for (var i = 1; i <= TargetCount; i++)
            {
                var seed = PinSeeds[(i - 1) % PinSeeds.Length];
                var encoded = IndoorMapGeometry.EncodeLayerPoint(seed.LayerId, seed.X, seed.Y);
                pins.Add(new EventPin
                {
                    Title = $"{seed.Title} #{i}",
                    Description = seed.Description,
                    Latitude = encoded.Latitude,
                    Longitude = encoded.Longitude,
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

        private User CreateUser(string username, string email, Role role, int? gradeLevel = null)
        {
            var schoolYearStart = gradeLevel.HasValue ? DetermineSchoolYearStart(DateTime.UtcNow) : (int?)null;
            var user = new User
            {
                Username = username,
                Email = email,
                Role = role,
                IsDeleted = false,
                IsBanned = false,
                PhotoUrl = null,
                GradeLevel = gradeLevel,
                SchoolYearStart = schoolYearStart,
                ScheduledDeletionAt = gradeLevel.HasValue && schoolYearStart.HasValue
                    ? CalculateScheduledDeletionUtc(gradeLevel.Value, schoolYearStart.Value)
                    : null
            };

            user.PasswordHash = _passwordHasher.HashPassword(user, "Test123!");
            return user;
        }

        private static int DetermineSchoolYearStart(DateTime referenceUtc)
        {
            var boundary = new DateTime(referenceUtc.Year, 9, 15, 0, 0, 0, DateTimeKind.Utc);
            return referenceUtc >= boundary ? referenceUtc.Year : referenceUtc.Year - 1;
        }

        private static DateTime CalculateScheduledDeletionUtc(int gradeLevel, int schoolYearStart)
        {
            var completionYear = schoolYearStart + 1;
            var completionDate = gradeLevel switch
            {
                12 => new DateTime(completionYear, 5, 15, 0, 0, 0, DateTimeKind.Utc),
                <= 3 => new DateTime(completionYear, 5, 29, 0, 0, 0, DateTimeKind.Utc),
                <= 6 => new DateTime(completionYear, 6, 12, 0, 0, 0, DateTimeKind.Utc),
                _ => new DateTime(completionYear, 6, 30, 0, 0, 0, DateTimeKind.Utc)
            };

            return completionDate.AddDays(1);
        }
    }
}
