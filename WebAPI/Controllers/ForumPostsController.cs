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
            if (user == null)
                return ToApiValidationFail("Authenticated user not found.", 401);

            var post = new ForumPost
            {
                Title = dto.Title,
                PhotoUrl = await SavePhotoAsync(dto.Photo),
                Content = dto.Content,
                Thread = thread,
                User = user,
                CreatedAt = DateTime.UtcNow
            };

            if (parentPost != null)
                parentPost.Replies.Add(post);
            else
                _db.ForumPosts.Add(post);

            await _db.SaveChangesAsync();

            var response = new ForumPostDto
            {
                Id = post.Id,
                Title = post.Title,
                PhotoUrl = post.PhotoUrl,
                Content = post.Content,
                CreatedAt = post.CreatedAt,
                UpdatedAt = post.UpdatedAt,
                UserId = post.User.Id,
                ThreadId = thread.Id,
                ParentPostId = dto.ParentPostId,
                Upvotes = 0,
                Downvotes = 0,
                Score = 0,
                MyVote = 0
            };

            return ToApiValidationSuccess(response, "Post created successfully.");
        }

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

            var mapped = posts.Select(x => new ForumPostDto
            {
                Id = x.Post.Id,
                Title = x.Post.Title,
                PhotoUrl = x.Post.PhotoUrl,
                Content = x.Post.Content,
                CreatedAt = x.Post.CreatedAt,
                UpdatedAt = x.Post.UpdatedAt,
                UserId = x.UserId,
                ThreadId = x.ThreadId,
                ParentPostId = x.Post.ParentPostId,
                Upvotes = x.Upvotes,
                Downvotes = x.Downvotes,
                Score = x.Upvotes - x.Downvotes,
                MyVote = x.MyVote
            }).ToList();

            var response = new PagedResponse<ForumPostDto>
            {
                Items = mapped,
                Page = paging.Page,
                PageSize = paging.PageSize,
                TotalCount = totalCount
            };

            return ToApiValidationSuccess(response);
        }

        [HttpPost("{id:int}/vote")]
        public async Task<IActionResult> Vote(int id, [FromBody] VoteRequestDto dto)
        {
            if (dto.Value != 1 && dto.Value != -1)
                return ToApiValidationFail("Vote must be 1 or -1.", 400);

            var post = await _db.ForumPosts.FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
            if (post == null)
                return ToApiValidationFail("Post not found.", 404);

            var user = await _db.Users.FindAsync(_authUser.Id);
            if (user == null)
                return ToApiValidationFail("User not found.", 401);

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

            return ToApiValidationSuccess("Vote updated.");
        }

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
