using Data;
using Data.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
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

        // ---------------- Create post / reply ----------------
        [HttpPost]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Create([FromForm] CreateForumPostForm dto)
        {
            if (!ModelState.IsValid)
                return ToApiValidationFail("Invalid post data.");

            var thread = await _db.ForumThreads.FindAsync(dto.ThreadId);
            if (thread == null)
                return ToApiValidationFail("Thread not found.", 404);

            if (thread.IsLocked)
                return ToApiValidationFail("Thread is locked.");

            ForumPost? parentPost = null;
            if (dto.ParentPostId.HasValue)
            {
                parentPost = await _db.ForumPosts
                    .Include(p => p.Replies)
                    .FirstOrDefaultAsync(p => p.Id == dto.ParentPostId);

                if (parentPost == null)
                    return ToApiValidationFail("Parent post not found.", 404);
            }

            var user = await _db.Users.FindAsync(_authUser.Id);

            var post = new ForumPost
            {
                Title = dto.Title,
                PhotoUrl = await SavePhotoAsync(dto.Photo),
                Content = dto.Content,
                Thread = thread,
                User = user!,
                CreatedAt = DateTime.UtcNow
            };

            if (parentPost != null)
                parentPost.Replies.Add(post);
            else
                _db.ForumPosts.Add(post);

            await _db.SaveChangesAsync();

            var postDto = new ForumPostDto
            {
                Id = post.Id,
                Title = post.Title,
                PhotoUrl = post.PhotoUrl,
                Content = post.Content,
                CreatedAt = post.CreatedAt,
                UpdatedAt = post.UpdatedAt,
                UserId = post.User!.Id,
                ThreadId = thread.Id,
                ParentPostId = dto.ParentPostId
            };

            return ToApiValidationSuccess(postDto, "Post created successfully.");
        }

        // ---------------- Get posts by thread ----------------

        [HttpGet("thread/{threadId:int}")]
        public async Task<IActionResult> GetByThread(
    int threadId,
    [FromQuery] PagingQuery paging)
        {
            var query = _db.ForumPosts
                .Where(p => p.Thread.Id == threadId && !p.IsDeleted);

            var totalCount = await query.CountAsync();

            var posts = await query
                .OrderBy(p => p.CreatedAt)
                .Skip(paging.Skip)
                .Take(paging.PageSize)
                .Select(p => new ForumPostDto
                {
                    Id = p.Id,
                    Title = p.Title,
                    Content = p.Content,
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt,
                    UserId = p.User.Id,
                    ThreadId = p.Thread.Id,
                    ParentPostId = p.ParentPostId
                })
                .ToListAsync();

            var response = new PagedResponse<ForumPostDto>
            {
                Items = posts,
                Page = paging.Page,
                PageSize = paging.PageSize,
                TotalCount = totalCount
            };

            return ToApiValidationSuccess(response);
        }

        // ---------------- Delete post ----------------

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var post = await _db.ForumPosts
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (post == null)
                return ToApiValidationFail("Post not found.", 404);

            if (_authUser.Role == Role.Student && post.User.Id != _authUser.Id)
                return ToApiValidationFail("You can't delete other users' posts", 400);


            post.IsDeleted = true;
            post.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            return ToApiValidationSuccess("Post deleted successfully.");
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
            return $"{baseUrl}/{relativePath.Replace("\\\\", "/")}/{fileName}";
        }
    }
}
