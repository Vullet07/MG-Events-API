using Data;
using Data.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Services.AuthUserService;
using Services.Dtos;
using Services.Maps;
using WebAPI.Extensions;
using WebAPI.Services.Accounts;

namespace WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class UserController : ApiControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IPasswordHasher<User> _passwordHasher;
        private readonly IAuthUserService _authUser;
        private readonly IWebHostEnvironment _env;
        private readonly IUserLifecycleService _userLifecycleService;

        public UserController(
            AppDbContext db,
            IPasswordHasher<User> passwordHasher,
            IAuthUserService authUser,
            IWebHostEnvironment env,
            IUserLifecycleService userLifecycleService)
        {
            _db = db;
            _passwordHasher = passwordHasher;
            _authUser = authUser;
            _env = env;
            _userLifecycleService = userLifecycleService;
        }

        [Authorize(Roles = "Admin,Teacher")]
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] PagingQuery paging)
        {
            var query = _db.Users.AsQueryable();
            if (_authUser.Role == Role.Teacher)
                query = query.Where(u => u.Role == Role.Student);

            var search = HttpContext.Request.Query["search"].ToString().Trim();
            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(u => u.Username.Contains(search) || u.Email.Contains(search));
            }

            var roleFilter = HttpContext.Request.Query["role"].ToString().Trim();
            if (Enum.TryParse<Role>(roleFilter, true, out var parsedRole))
            {
                query = query.Where(u => u.Role == parsedRole);
            }

            var gradeLevelRaw = HttpContext.Request.Query["gradeLevel"].ToString().Trim();
            if (int.TryParse(gradeLevelRaw, out var gradeLevel))
            {
                query = query.Where(u => u.GradeLevel == gradeLevel);
            }

            var accountState = HttpContext.Request.Query["accountState"].ToString().Trim().ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(accountState))
            {
                var now = DateTime.UtcNow;
                query = accountState switch
                {
                    "banned" => query.Where(u => u.IsBanned && (u.BannedUntil == null || u.BannedUntil > now)),
                    "active" => query.Where(u => (!u.IsBanned || (u.BannedUntil != null && u.BannedUntil <= now)) && u.IsEmailConfirmed),
                    "expiring" => query.Where(u => u.ScheduledDeletionAt != null && u.ScheduledDeletionAt <= now.AddDays(45)),
                    "protected" => query.Where(u => u.Role == Role.Admin),
                    "unconfirmed" => query.Where(u => !u.IsEmailConfirmed),
                    _ => query
                };
            }

            var sort = HttpContext.Request.Query["sort"].ToString().Trim().ToLowerInvariant();
            query = sort switch
            {
                "role" => query.OrderBy(u => u.Role).ThenBy(u => u.Username),
                "grade" => query.OrderBy(u => u.GradeLevel).ThenBy(u => u.Username),
                "scheduleddeletion" => query.OrderBy(u => u.ScheduledDeletionAt).ThenBy(u => u.Username),
                _ => query.OrderBy(u => u.Username)
            };

            var totalCount = await query.CountAsync();

            var users = await query
                .Skip(paging.Skip)
                .Take(paging.PageSize)
                .ToListAsync();

            var mapped = users.Select(u => ToUserDto(u, true)).ToList();

            var response = new PagedResponse<UserDto>
            {
                Items = mapped,
                Page = paging.Page,
                PageSize = paging.PageSize,
                TotalCount = totalCount
            };

            return ToApiValidationSuccess(response);
        }

        [HttpGet("profile")]
        public async Task<IActionResult> GetMyProfile()
        {
            if (_authUser.Id == null)
                return ToApiValidationFail("User not authenticated.", 401);

            var user = await _db.Users.FindAsync(_authUser.Id.Value);
            if (user == null)
                return ToApiValidationFail("User not found.", 404);

            return ToApiValidationSuccess(ToUserDto(user, true));
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var user = await _db.Users.FindAsync(id);
            if (user == null)
                return ToApiValidationFail("User not found.", 404);

            return ToApiValidationSuccess(ToUserDto(user, true));
        }

        [HttpGet("public/{id:int}")]
        public async Task<IActionResult> GetPublicById(int id)
        {
            var user = await _db.Users.FindAsync(id);
            if (user == null)
                return ToApiValidationFail("User not found.", 404);

            return ToApiValidationSuccess(ToUserDto(user, true));
        }

        [HttpGet("public/{id:int}/threads")]
        public async Task<IActionResult> GetPublicThreads(int id, [FromQuery] PagingQuery paging)
        {
            var userExists = await _db.Users.AnyAsync(u => u.Id == id);
            if (!userExists)
                return ToApiValidationFail("User not found.", 404);

            var query = _db.ForumThreads
                .Where(t => EF.Property<int>(t, "CreatedByUserId") == id);

            var totalCount = await query.CountAsync();
            var items = await query
                .OrderByDescending(t => t.LastPostAt ?? t.CreatedAt)
                .Skip(paging.Skip)
                .Take(paging.PageSize)
                .Select(t => new PublicUserThreadItemDto
                {
                    ThreadId = t.Id,
                    Title = t.Title,
                    CreatedAt = t.CreatedAt,
                    LastPostAt = t.LastPostAt,
                    IsPinned = t.IsPinned,
                    IsLocked = t.IsLocked
                })
                .ToListAsync();

            return ToApiValidationSuccess(new PagedResponse<PublicUserThreadItemDto>
            {
                Items = items,
                Page = paging.Page,
                PageSize = paging.PageSize,
                TotalCount = totalCount
            });
        }

        [HttpGet("public/{id:int}/posts")]
        public async Task<IActionResult> GetPublicPosts(int id, [FromQuery] PagingQuery paging)
        {
            var userExists = await _db.Users.AnyAsync(u => u.Id == id);
            if (!userExists)
                return ToApiValidationFail("User not found.", 404);

            var query = _db.ForumPosts
                .Where(p => !p.IsDeleted && EF.Property<int>(p, "UserId") == id);

            var totalCount = await query.CountAsync();
            var items = await query
                .OrderByDescending(p => p.CreatedAt)
                .Skip(paging.Skip)
                .Take(paging.PageSize)
                .Select(p => new PublicUserPostItemDto
                {
                    PostId = p.Id,
                    ThreadId = EF.Property<int>(p, "ThreadId"),
                    ThreadTitle = p.Thread.Title,
                    Title = p.Title,
                    Content = p.Content,
                    PhotoUrl = p.PhotoUrl,
                    CreatedAt = p.CreatedAt,
                    ParentPostId = p.ParentPostId,
                    Upvotes = _db.PostVotes.Count(v => v.Post.Id == p.Id && v.Value == VoteValue.Up),
                    Downvotes = _db.PostVotes.Count(v => v.Post.Id == p.Id && v.Value == VoteValue.Down)
                })
                .ToListAsync();

            return ToApiValidationSuccess(new PagedResponse<PublicUserPostItemDto>
            {
                Items = items,
                Page = paging.Page,
                PageSize = paging.PageSize,
                TotalCount = totalCount
            });
        }

        [HttpGet("public/{id:int}/pins")]
        public async Task<IActionResult> GetPublicPins(int id, [FromQuery] PagingQuery paging)
        {
            var userExists = await _db.Users.AnyAsync(u => u.Id == id);
            if (!userExists)
                return ToApiValidationFail("User not found.", 404);

            var query = _db.EventPins
                .Where(p => EF.Property<int>(p, "CreatedByUserId") == id && !p.IsResolved);

            var totalCount = await query.CountAsync();
            var items = await query
                .OrderByDescending(p => p.CreatedAt)
                .Skip(paging.Skip)
                .Take(paging.PageSize)
                .Select(p => new
                {
                    Pin = p,
                    Upvotes = _db.PinVotes.Count(v => v.Pin.Id == p.Id && v.Value == VoteValue.Up),
                    Downvotes = _db.PinVotes.Count(v => v.Pin.Id == p.Id && v.Value == VoteValue.Down)
                })
                .ToListAsync();

            var mappedItems = items.Select(item =>
            {
                IndoorMapGeometry.TryResolveZone(item.Pin.Latitude, item.Pin.Longitude, out var zone);

                return new PublicUserPinItemDto
                {
                    PinId = item.Pin.Id,
                    Title = item.Pin.Title,
                    Description = item.Pin.Description,
                    Category = item.Pin.Category,
                    PhotoUrl = item.Pin.PhotoUrl,
                    Latitude = item.Pin.Latitude,
                    Longitude = item.Pin.Longitude,
                    LayerLabel = zone?.LayerLabel ?? "Неизвестен слой",
                    ZoneLabel = zone?.ZoneLabel ?? "Неизвестна зона",
                    CreatedAt = item.Pin.CreatedAt,
                    Upvotes = item.Upvotes,
                    Downvotes = item.Downvotes
                };
            }).ToList();

            return ToApiValidationSuccess(new PagedResponse<PublicUserPinItemDto>
            {
                Items = mappedItems,
                Page = paging.Page,
                PageSize = paging.PageSize,
                TotalCount = totalCount
            });
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateUserDto dto)
        {
            var user = await _db.Users.FindAsync(id);
            if (user == null)
                return ToApiValidationFail("User not found.", 404);

            if (_authUser.Role != Role.Admin && user.Id != _authUser.Id)
                return ToApiValidationFail("Only admins can update other users' info", 400);

            if (!string.IsNullOrEmpty(dto.Email))
            {
                var normalizedEmail = dto.Email.Trim().ToLowerInvariant();
                if (await _db.Users.AnyAsync(u => u.Email == normalizedEmail && u.Id != id))
                    return ToApiValidationFail("Email is already used by another user.", 400);

                user.Email = normalizedEmail;
            }

            if (!string.IsNullOrWhiteSpace(dto.Username))
            {
                var username = dto.Username.Trim();
                if (await _db.Users.AnyAsync(u => u.Username == username && u.Id != id))
                    return ToApiValidationFail("Username is already used by another user.", 400);
                user.Username = username;
            }

            if (!string.IsNullOrEmpty(dto.PhotoUrl))
                user.PhotoUrl = dto.PhotoUrl;

            if (dto.Role.HasValue)
                user.Role = dto.Role.Value;

            if (!string.IsNullOrEmpty(dto.Password))
                user.PasswordHash = _passwordHasher.HashPassword(user, dto.Password);

            await _db.SaveChangesAsync();

            return ToApiValidationSuccess(ToUserDto(user, true), "User updated successfully.");
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("teacher-requests")]
        public async Task<IActionResult> GetTeacherRequests()
        {
            var requests = await _db.TeacherRegistrationRequests
                .Where(r => r.IsEmailConfirmed)
                .Include(r => r.ReviewedBy)
                .OrderBy(r => r.Status)
                .ThenByDescending(r => r.CreatedAt)
                .Select(r => new TeacherRegistrationRequestDto
                {
                    Id = r.Id,
                    Username = r.Username,
                    Email = r.Email,
                    IsEmailConfirmed = r.IsEmailConfirmed,
                    EmailConfirmedAt = r.EmailConfirmedAt,
                    Motivation = r.Motivation,
                    Status = r.Status,
                    CreatedAt = r.CreatedAt,
                    ReviewedAt = r.ReviewedAt,
                    ReviewNote = r.ReviewNote,
                    ReviewedByUsername = r.ReviewedBy != null ? r.ReviewedBy.Username : null
                })
                .ToListAsync();

            return ToApiValidationSuccess(requests);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("teacher-requests/{id:int}/approve")]
        public async Task<IActionResult> ApproveTeacherRequest(int id, [FromBody] ReviewTeacherRegistrationRequestDto dto)
        {
            var request = await _db.TeacherRegistrationRequests.FirstOrDefaultAsync(r => r.Id == id);
            if (request == null)
                return ToApiValidationFail("Teacher request not found.", 404);

            if (request.Status != TeacherRegistrationStatus.Pending)
                return ToApiValidationFail("Teacher request is already reviewed.", 400);

            if (!request.IsEmailConfirmed)
                return ToApiValidationFail("Teacher request must confirm the school email before approval.", 400);

            if (await _db.Users.AnyAsync(u => u.Username == request.Username))
                return ToApiValidationFail("Cannot approve because username is already in use.", 409);

            if (await _db.Users.AnyAsync(u => u.Email == request.Email))
                return ToApiValidationFail("Cannot approve because email is already in use.", 409);

            var teacher = new User
            {
                Username = request.Username,
                Email = request.Email,
                Role = Role.Teacher,
                PasswordHash = request.PasswordHash,
                IsEmailConfirmed = true,
                EmailConfirmedAt = request.EmailConfirmedAt ?? DateTime.UtcNow
            };

            request.Status = TeacherRegistrationStatus.Approved;
            request.ReviewedAt = DateTime.UtcNow;
            request.ReviewNote = dto.Note?.Trim();
            if (_authUser.Id.HasValue)
                request.ReviewedBy = await _db.Users.FindAsync(_authUser.Id.Value);

            _db.Users.Add(teacher);
            await _db.SaveChangesAsync();

            return ToApiValidationSuccess("Teacher request approved and account created.");
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("teacher-requests/{id:int}/reject")]
        public async Task<IActionResult> RejectTeacherRequest(int id, [FromBody] ReviewTeacherRegistrationRequestDto dto)
        {
            var request = await _db.TeacherRegistrationRequests.FirstOrDefaultAsync(r => r.Id == id);
            if (request == null)
                return ToApiValidationFail("Teacher request not found.", 404);

            if (request.Status != TeacherRegistrationStatus.Pending)
                return ToApiValidationFail("Teacher request is already reviewed.", 400);

            if (!request.IsEmailConfirmed)
                return ToApiValidationFail("Teacher request must confirm the school email before review.", 400);

            request.Status = TeacherRegistrationStatus.Rejected;
            request.ReviewedAt = DateTime.UtcNow;
            request.ReviewNote = dto.Note?.Trim();
            if (_authUser.Id.HasValue)
                request.ReviewedBy = await _db.Users.FindAsync(_authUser.Id.Value);

            await _db.SaveChangesAsync();

            return ToApiValidationSuccess("Teacher request rejected.");
        }

        [HttpPost("profile-photo")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadProfilePhoto(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return ToApiValidationFail("No file uploaded.");

            var userId = _authUser.Id;
            if (userId == null)
                return ToApiValidationFail("User not authenticated.", 401);

            var user = await _db.Users.FindAsync(userId);
            if (user == null)
                return ToApiValidationFail("User not found.", 404);

            var webRoot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
            var relativePath = Path.Combine("uploads", "users", userId.Value.ToString());
            var savePath = Path.Combine(webRoot, relativePath);
            Directory.CreateDirectory(savePath);

            var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
            var fullPath = Path.Combine(savePath, fileName);

            await using (var stream = System.IO.File.Create(fullPath))
            {
                await file.CopyToAsync(stream);
            }

            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            user.PhotoUrl = $"{baseUrl}/{relativePath.Replace("\\", "/")}/{fileName}";
            await _db.SaveChangesAsync();

            return ToApiValidationSuccess(ToUserDto(user, true), "Profile photo updated.");
        }

        [Authorize(Roles = "Admin,Teacher")]
        [HttpPost("{id:int}/ban")]
        public async Task<IActionResult> BanUser(int id, [FromBody] BanUserDto dto)
        {
            var user = await _db.Users.FindAsync(id);
            if (user == null)
                return ToApiValidationFail("User not found.", 404);

            if (user.Role == Role.Admin || user.Role == Role.Teacher)
                return ToApiValidationFail("You can't ban admins or teachers.", 403);

            if (_authUser.Id == user.Id)
                return ToApiValidationFail("You can't ban yourself.", 403);

            if (user.IsBanned)
                return ToApiValidationFail("User is already banned.");

            user.IsBanned = true;
            user.BannedUntil = dto.BannedUntil;

            await _db.SaveChangesAsync();

            return ToApiValidationSuccess(new
            {
                user.Id,
                user.Username,
                user.IsBanned,
                user.BannedUntil
            }, "User banned successfully.");
        }

        [Authorize(Roles = "Admin,Teacher")]
        [HttpPost("{id:int}/unban")]
        public async Task<IActionResult> UnbanUser(int id)
        {
            var user = await _db.Users.FindAsync(id);
            if (user == null)
                return ToApiValidationFail("User not found.", 404);

            if (user.Role == Role.Admin || user.Role == Role.Teacher)
                return ToApiValidationFail("You can't unban admins or teachers.", 403);

            if (_authUser.Id == user.Id)
                return ToApiValidationFail("You can't unban yourself.", 403);

            if (!user.IsBanned)
                return ToApiValidationFail("User is not banned.");

            user.IsBanned = false;
            user.BannedUntil = null;

            await _db.SaveChangesAsync();

            return ToApiValidationSuccess(new
            {
                user.Id,
                user.Username,
                user.IsBanned
            }, "User unbanned successfully.");
        }

        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var user = await _db.Users.FindAsync(id);
            if (user == null)
                return ToApiValidationFail("User not found.", 404);

            if (user.Role == Role.Admin)
                return ToApiValidationFail("Admin accounts cannot be deleted.", 403);

            if (_authUser.Id == user.Id)
                return ToApiValidationFail("You can't delete your own account from the admin dashboard.", 403);

            var deletionSummary = await _userLifecycleService.DeleteUserWithContentAsync(user.Id);
            if (deletionSummary == null)
                return ToApiValidationFail("User not found.", 404);

            return ToApiValidationSuccess(
                $"User deleted successfully. Removed {deletionSummary.RemovedThreads} threads, {deletionSummary.RemovedPosts} standalone posts and {deletionSummary.RemovedPins} pins.");
        }

        private UserDto ToUserDto(User user, bool includeStats)
        {
            var dto = new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                Role = user.Role,
                IsEmailConfirmed = user.IsEmailConfirmed,
                PhotoUrl = user.PhotoUrl,
                IsBanned = user.IsBanned && (user.BannedUntil == null || user.BannedUntil > DateTime.UtcNow),
                BannedUntil = user.BannedUntil,
                GradeLevel = user.GradeLevel,
                SchoolYearStart = user.SchoolYearStart,
                ScheduledDeletionAt = user.ScheduledDeletionAt
            };

            if (includeStats)
            {
                dto.ThreadsCount = _db.ForumThreads.Count(t => t.CreatedByUser.Id == user.Id);
                dto.PostsCount = _db.ForumPosts.Count(p => p.User.Id == user.Id && !p.IsDeleted);
                dto.PinsCount = _db.EventPins.Count(p => p.CreatedByUser.Id == user.Id && !p.IsResolved);
            }

            return dto;
        }
    }
}
