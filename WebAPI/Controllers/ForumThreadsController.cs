using Data;
using Data.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Services.AuthUserService;
using Services.Dtos;
using WebAPI.Extensions;

namespace WebAPI.Controllers
{
    [ApiController]
    [Route("api/forum-threads")]
    public class ForumThreadsController : ApiControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IAuthUserService _authUser;
        private readonly IWebHostEnvironment _env;

        public ForumThreadsController(AppDbContext db, IAuthUserService authUser, IWebHostEnvironment env)
        {
            _db = db;
            _authUser = authUser;
            _env = env;
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateForumThreadDto dto)
        {
            if (!ModelState.IsValid || string.IsNullOrWhiteSpace(dto.Title))
                return ToApiValidationFail("Невалидни данни за тема.");

            if (IsNewsThread(dto.Title) && !IsModerator())
                return ToApiValidationFail("Само учители и администратори могат да публикуват новини.", 403);

            var user = await _db.Users.FindAsync(_authUser.Id);
            if (user == null)
                return ToApiValidationFail("Липсва удостоверен потребител.", 401);

            var thread = new ForumThread
            {
                Title = dto.Title.Trim(),
                CreatedByUser = user,
                CreatedAt = DateTime.UtcNow,
                IsLocked = false,
                IsPinned = false
            };

            _db.ForumThreads.Add(thread);
            await _db.SaveChangesAsync();

            return ToApiValidationSuccess(ToDto(thread), "Темата е създадена успешно.");
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] PagingQuery paging)
        {
            var query = _db.ForumThreads
                .Include(t => t.CreatedByUser)
                .AsQueryable();

            var totalCount = await query.CountAsync();

            var threads = await query
                .OrderByDescending(t => t.IsPinned)
                .ThenByDescending(t => t.LastPostAt ?? t.CreatedAt)
                .Skip(paging.Skip)
                .Take(paging.PageSize)
                .ToListAsync();

            var response = new PagedResponse<ForumThreadDto>
            {
                Items = threads.Select(ToDto).ToList(),
                Page = paging.Page,
                PageSize = paging.PageSize,
                TotalCount = totalCount
            };

            return ToApiValidationSuccess(response);
        }

        [AllowAnonymous]
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var thread = await _db.ForumThreads
                .Include(t => t.CreatedByUser)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (thread == null)
                return ToApiValidationFail("Темата не е намерена.", 404);

            return ToApiValidationSuccess(ToDto(thread));
        }

        [Authorize]
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateForumThreadDto dto)
        {
            var thread = await LoadThreadAsync(id);
            if (thread == null)
                return ToApiValidationFail("Темата не е намерена.", 404);

            if (!CanManageThread(thread))
                return ToApiValidationFail("Нямаш права да редактираш тази тема.", 403);

            if (string.IsNullOrWhiteSpace(dto.Title))
                return ToApiValidationFail("Заглавието е задължително.");

            if (IsNewsThread(thread.Title) && !IsModerator())
                return ToApiValidationFail("Само учители и администратори могат да редактират новини.", 403);

            if (IsNewsThread(dto.Title) && !IsModerator())
                return ToApiValidationFail("Само учители и администратори могат да публикуват новини.", 403);

            thread.Title = dto.Title.Trim();
            await _db.SaveChangesAsync();

            return ToApiValidationSuccess(ToDto(thread), "Темата е обновена.");
        }

        [Authorize]
        [HttpPut("{id:int}/lock")]
        public async Task<IActionResult> Lock(int id)
        {
            var thread = await LoadThreadAsync(id);
            if (thread == null)
                return ToApiValidationFail("Темата не е намерена.", 404);

            if (!CanManageThread(thread))
                return ToApiValidationFail("Нямаш права да заключиш тази тема.", 403);

            thread.IsLocked = true;
            await _db.SaveChangesAsync();

            return ToApiValidationSuccess("Темата е заключена.");
        }

        [Authorize]
        [HttpPut("{id:int}/unlock")]
        public async Task<IActionResult> Unlock(int id)
        {
            var thread = await LoadThreadAsync(id);
            if (thread == null)
                return ToApiValidationFail("Темата не е намерена.", 404);

            if (!CanManageThread(thread))
                return ToApiValidationFail("Нямаш права да отключиш тази тема.", 403);

            thread.IsLocked = false;
            await _db.SaveChangesAsync();

            return ToApiValidationSuccess("Темата е отключена.");
        }

        [Authorize]
        [HttpPut("{id:int}/pin")]
        public async Task<IActionResult> Pin(int id)
        {
            if (!IsModerator())
                return ToApiValidationFail("Само учители и администратори могат да закачат теми.", 403);

            var thread = await LoadThreadAsync(id);
            if (thread == null)
                return ToApiValidationFail("Темата не е намерена.", 404);

            thread.IsPinned = true;
            await _db.SaveChangesAsync();

            return ToApiValidationSuccess("Темата е закачена.");
        }

        [Authorize]
        [HttpPut("{id:int}/unpin")]
        public async Task<IActionResult> Unpin(int id)
        {
            if (!IsModerator())
                return ToApiValidationFail("Само учители и администратори могат да откачат теми.", 403);

            var thread = await LoadThreadAsync(id);
            if (thread == null)
                return ToApiValidationFail("Темата не е намерена.", 404);

            thread.IsPinned = false;
            await _db.SaveChangesAsync();

            return ToApiValidationSuccess("Темата е откачена.");
        }

        [Authorize]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var thread = await LoadThreadAsync(id);
            if (thread == null)
                return ToApiValidationFail("Темата не е намерена.", 404);

            if (!CanManageThread(thread))
                return ToApiValidationFail("Нямаш права да изтриеш тази тема.", 403);

            var posts = await _db.ForumPosts
                .Where(p => EF.Property<int>(p, "ThreadId") == id)
                .ToListAsync();

            foreach (var post in posts.Where(post => post.ParentPostId != null))
            {
                post.ParentPostId = null;
            }

            if (posts.Count > 0)
            {
                await _db.SaveChangesAsync();
            }

            var postIds = posts.Select(post => post.Id).ToList();
            var relatedReports = await _db.Reports
                .Where(report =>
                    (report.TargetType == ReportTargetType.Thread && report.TargetId == id)
                    || (report.TargetType == ReportTargetType.Post && postIds.Contains(report.TargetId)))
                .ToListAsync();

            if (relatedReports.Count > 0)
            {
                _db.Reports.RemoveRange(relatedReports);
            }

            foreach (var post in posts)
            {
                DeleteLocalMedia(post.PhotoUrl);
            }

            _db.ForumThreads.Remove(thread);
            await _db.SaveChangesAsync();

            return ToApiValidationSuccess("Темата е изтрита.");
        }

        private async Task<ForumThread?> LoadThreadAsync(int id)
        {
            return await _db.ForumThreads
                .Include(t => t.CreatedByUser)
                .FirstOrDefaultAsync(t => t.Id == id);
        }

        private bool CanManageThread(ForumThread thread)
        {
            if (_authUser.Id == null)
            {
                return false;
            }

            return _authUser.Id == thread.CreatedByUser.Id || IsModerator();
        }

        private bool IsModerator() => _authUser.Role == Role.Admin || _authUser.Role == Role.Teacher;

        private static bool IsNewsThread(string? title)
        {
            var normalized = title?.Trim() ?? string.Empty;
            return normalized.StartsWith("[news]", StringComparison.OrdinalIgnoreCase)
                   || normalized.StartsWith("[новина]", StringComparison.OrdinalIgnoreCase);
        }

        private static ForumThreadDto ToDto(ForumThread thread)
        {
            return new ForumThreadDto
            {
                Id = thread.Id,
                Title = thread.Title,
                IsLocked = thread.IsLocked,
                IsPinned = thread.IsPinned,
                CreatedAt = thread.CreatedAt,
                LastPostAt = thread.LastPostAt,
                CreatedByUserId = thread.CreatedByUser.Id,
                CreatedByUsername = thread.CreatedByUser.Username,
                CreatedByRole = thread.CreatedByUser.Role.ToString()
            };
        }

        private void DeleteLocalMedia(string? mediaUrl)
        {
            if (string.IsNullOrWhiteSpace(mediaUrl))
                return;

            var webRoot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
            var relativePath = Uri.TryCreate(mediaUrl, UriKind.Absolute, out var absoluteUri)
                ? absoluteUri.AbsolutePath.TrimStart('/')
                : mediaUrl.TrimStart('/', '\\');

            var fullPath = Path.GetFullPath(Path.Combine(webRoot, relativePath.Replace("/", Path.DirectorySeparatorChar.ToString())));
            var rootPath = Path.GetFullPath(webRoot);

            if (!fullPath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase))
                return;

            if (System.IO.File.Exists(fullPath))
            {
                System.IO.File.Delete(fullPath);
            }
        }
    }
}
