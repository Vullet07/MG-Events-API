using Data;
using Data.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Services.AuthUserService;
using Services.Dtos;
using WebAPI.Extensions;
using WebAPI.Models;

namespace WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ForumPostsController : ApiControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IAuthUserService _authUser;
        private readonly IWebHostEnvironment _env;

        public ForumPostsController(AppDbContext db, IAuthUserService authUser, IWebHostEnvironment env)
        {
            _db = db;
            _authUser = authUser;
            _env = env;
        }

        [HttpPost]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Create([FromForm] CreateForumPostForm dto)
        {
            if (!ModelState.IsValid || string.IsNullOrWhiteSpace(dto.Title))
                return ToApiValidationFail("Невалидни данни за публикация.");

            var thread = await _db.ForumThreads.FindAsync(dto.ThreadId);
            if (thread == null)
                return ToApiValidationFail("Темата не е намерена.", 404);

            if (thread.IsLocked)
                return ToApiValidationFail("Темата е заключена.");

            ForumPost? parentPost = null;
            if (dto.ParentPostId.HasValue)
            {
                parentPost = await _db.ForumPosts
                    .Include(p => p.Thread)
                    .FirstOrDefaultAsync(p => p.Id == dto.ParentPostId.Value && !p.IsDeleted);

                if (parentPost == null)
                    return ToApiValidationFail("Родителската публикация не е намерена.", 404);

                if (parentPost.Thread.Id != dto.ThreadId)
                    return ToApiValidationFail("Отговорът трябва да бъде в същата тема.", 400);
            }

            if (dto.Photo != null && !dto.Photo.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                return ToApiValidationFail("Към публикация може да се качва само изображение.", 400);

            var user = await _db.Users.FindAsync(_authUser.Id);
            if (user == null)
                return ToApiValidationFail("Липсва удостоверен потребител.", 401);

            var post = new ForumPost
            {
                Title = dto.Title.Trim(),
                PhotoUrl = await SavePhotoAsync(dto.Photo),
                Content = dto.Content?.Trim() ?? string.Empty,
                Thread = thread,
                User = user,
                CreatedAt = DateTime.UtcNow,
                ParentPost = parentPost
            };

            _db.ForumPosts.Add(post);
            thread.LastPostAt = post.CreatedAt;
            await _db.SaveChangesAsync();

            return ToApiValidationSuccess(ToDto(post, thread.Id, user.Id, 0, 0, 0), "Публикацията е създадена успешно.");
        }

        [AllowAnonymous]
        [HttpGet("thread/{threadId:int}")]
        public async Task<IActionResult> GetByThread(int threadId, [FromQuery] PagingQuery paging)
        {
            var currentUserId = _authUser.Id;
            var query = _db.ForumPosts
                .Where(p => EF.Property<int>(p, "ThreadId") == threadId && !p.IsDeleted);

            var totalCount = await query.CountAsync();

            var posts = await query
                .Select(p => new
                {
                    Post = p,
                    UserId = EF.Property<int>(p, "UserId"),
                    ThreadId = EF.Property<int>(p, "ThreadId"),
                    Upvotes = _db.PostVotes.Count(v => v.Post.Id == p.Id && v.Value == VoteValue.Up),
                    Downvotes = _db.PostVotes.Count(v => v.Post.Id == p.Id && v.Value == VoteValue.Down),
                    MyVote = currentUserId == null
                        ? 0
                        : _db.PostVotes
                            .Where(v => v.Post.Id == p.Id && v.User.Id == currentUserId.Value)
                            .Select(v => (int?)v.Value)
                            .FirstOrDefault() ?? 0
                })
                .OrderByDescending(x => x.Upvotes - x.Downvotes)
                .ThenByDescending(x => x.Post.CreatedAt)
                .Skip(paging.Skip)
                .Take(paging.PageSize)
                .ToListAsync();

            var mapped = posts.Select(x => ToDto(x.Post, x.ThreadId, x.UserId, x.Upvotes, x.Downvotes, x.MyVote)).ToList();

            var response = new PagedResponse<ForumPostDto>
            {
                Items = mapped,
                Page = paging.Page,
                PageSize = paging.PageSize,
                TotalCount = totalCount
            };

            return ToApiValidationSuccess(response);
        }

        [HttpPut("{id:int}")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Update(int id, [FromForm] UpdateForumPostForm dto)
        {
            var post = await _db.ForumPosts
                .Include(p => p.User)
                .Include(p => p.Thread)
                .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

            if (post == null)
                return ToApiValidationFail("Публикацията не е намерена.", 404);

            if (!CanManagePost(post))
                return ToApiValidationFail("Нямаш права да редактираш тази публикация.", 403);

            if (string.IsNullOrWhiteSpace(dto.Title))
                return ToApiValidationFail("Съдържанието е задължително.");

            if (dto.Photo != null && !dto.Photo.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                return ToApiValidationFail("Към публикация може да се качва само изображение.", 400);

            post.Title = dto.Title.Trim();
            post.Content = dto.Content?.Trim() ?? string.Empty;
            post.UpdatedAt = DateTime.UtcNow;

            if (dto.RemovePhoto)
            {
                DeleteLocalMedia(post.PhotoUrl);
                post.PhotoUrl = null;
            }

            if (dto.Photo != null)
            {
                DeleteLocalMedia(post.PhotoUrl);
                post.PhotoUrl = await SavePhotoAsync(dto.Photo);
            }

            await _db.SaveChangesAsync();

            var upvotes = await _db.PostVotes.CountAsync(v => v.Post.Id == post.Id && v.Value == VoteValue.Up);
            var downvotes = await _db.PostVotes.CountAsync(v => v.Post.Id == post.Id && v.Value == VoteValue.Down);
            var myVote = _authUser.Id == null
                ? 0
                : await _db.PostVotes
                    .Where(v => v.Post.Id == post.Id && v.User.Id == _authUser.Id.Value)
                    .Select(v => (int?)v.Value)
                    .FirstOrDefaultAsync() ?? 0;

            return ToApiValidationSuccess(
                ToDto(post, post.Thread.Id, post.User.Id, upvotes, downvotes, myVote),
                "Публикацията е обновена.");
        }

        [HttpPost("{id:int}/vote")]
        public async Task<IActionResult> Vote(int id, [FromBody] VoteRequestDto dto)
        {
            if (dto.Value != 1 && dto.Value != -1)
                return ToApiValidationFail("Гласът трябва да е 1 или -1.", 400);

            var post = await _db.ForumPosts.FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
            if (post == null)
                return ToApiValidationFail("Публикацията не е намерена.", 404);

            var user = await _db.Users.FindAsync(_authUser.Id);
            if (user == null)
                return ToApiValidationFail("Потребителят не е намерен.", 401);

            var existing = await _db.PostVotes
                .FirstOrDefaultAsync(v => v.User.Id == user.Id && v.Post.Id == post.Id);

            if (existing == null)
            {
                _db.PostVotes.Add(new PostVote
                {
                    User = user,
                    Post = post,
                    Value = dto.Value == 1 ? VoteValue.Up : VoteValue.Down
                });
            }
            else if ((int)existing.Value == dto.Value)
            {
                _db.PostVotes.Remove(existing);
            }
            else
            {
                existing.Value = dto.Value == 1 ? VoteValue.Up : VoteValue.Down;
            }

            await _db.SaveChangesAsync();

            return ToApiValidationSuccess("Гласът е обновен.");
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var post = await _db.ForumPosts
                .Include(p => p.User)
                .Include(p => p.Thread)
                .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

            if (post == null)
                return ToApiValidationFail("Публикацията не е намерена.", 404);

            if (!CanManagePost(post))
                return ToApiValidationFail("Нямаш права да изтриеш тази публикация.", 403);

            DeleteLocalMedia(post.PhotoUrl);
            post.IsDeleted = true;
            post.PhotoUrl = null;
            post.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            var lastActivePostAt = await _db.ForumPosts
                .Where(item => EF.Property<int>(item, "ThreadId") == post.Thread.Id && !item.IsDeleted)
                .MaxAsync(item => (DateTime?)item.CreatedAt);

            post.Thread.LastPostAt = lastActivePostAt;
            await _db.SaveChangesAsync();

            return ToApiValidationSuccess("Публикацията е изтрита.");
        }

        private bool CanManagePost(ForumPost post)
        {
            if (_authUser.Id == null)
            {
                return false;
            }

            return _authUser.Id == post.User.Id
                   || _authUser.Role == Role.Admin
                   || _authUser.Role == Role.Teacher;
        }

        private static ForumPostDto ToDto(ForumPost post, int threadId, int userId, int upvotes, int downvotes, int myVote)
        {
            return new ForumPostDto
            {
                Id = post.Id,
                Title = post.Title,
                PhotoUrl = post.PhotoUrl,
                Content = post.Content,
                CreatedAt = post.CreatedAt,
                UpdatedAt = post.UpdatedAt,
                UserId = userId,
                ThreadId = threadId,
                ParentPostId = post.ParentPostId,
                Upvotes = upvotes,
                Downvotes = downvotes,
                Score = upvotes - downvotes,
                MyVote = myVote
            };
        }

        private async Task<string?> SavePhotoAsync(IFormFile? file)
        {
            if (file == null || file.Length == 0)
                return null;

            var userId = _authUser.Id?.ToString() ?? "unknown";
            var webRoot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
            var relativePath = Path.Combine("uploads", "posts", userId);
            var savePath = Path.Combine(webRoot, relativePath);
            Directory.CreateDirectory(savePath);

            var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
            var fullPath = Path.Combine(savePath, fileName);

            await using (var stream = System.IO.File.Create(fullPath))
            {
                await file.CopyToAsync(stream);
            }

            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            return $"{baseUrl}/{relativePath.Replace("\\", "/")}/{fileName}";
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
