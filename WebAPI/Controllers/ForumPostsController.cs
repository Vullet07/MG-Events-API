using Data;
using Data.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Services.AuthUserService;
using Services.Dtos;

namespace WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ForumPostsController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IAuthUserService _authUser;

        public ForumPostsController(AppDbContext db, IAuthUserService authUser)
        {
            _db = db;
            _authUser = authUser;
        }

        // ---------------- Create post / reply ----------------
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateForumPostDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var thread = await _db.ForumThreads.FindAsync(dto.ThreadId);
            if (thread == null)
                return NotFound("Thread not found.");
            if (thread.IsLocked)
                return BadRequest("Thread is locked.");

            ForumPost? parentPost = null;
            if (dto.ParentPostId.HasValue)
            {
                parentPost = await _db.ForumPosts
                    .Include(p => p.Replies)
                    .FirstOrDefaultAsync(p => p.Id == dto.ParentPostId);

                if (parentPost == null)
                    return NotFound("Parent post not found.");
            }

            var post = new ForumPost
            {
                Title = dto.Title,
                Content = dto.Content,
                Thread = thread,
                User = await _db.Users.FindAsync(_authUser.Id),
                CreatedAt = DateTime.UtcNow
            };

            if (parentPost != null)
                parentPost.Replies.Add(post);
            else
                _db.ForumPosts.Add(post);

            await _db.SaveChangesAsync();

            return Ok(new ForumPostDto
            {
                Id = post.Id,
                Title = post.Title,
                Content = post.Content,
                CreatedAt = post.CreatedAt,
                UpdatedAt = post.UpdatedAt,
                UserId = post.User!.Id,
                ThreadId = thread.Id
            });
        }

        [AllowAnonymous]
        [HttpGet("thread/{threadId:int}")]
        public async Task<IActionResult> GetByThread(int threadId)
        {
            var posts = await _db.ForumPosts
                .Where(p => p.Thread.Id == threadId && !p.IsDeleted)
                .OrderBy(p => p.CreatedAt)
                .Select(p => new ForumPostDto
                {
                    Id = p.Id,
                    Title = p.Title,
                    Content = p.Content,
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt,
                    UserId = p.User.Id,
                    ThreadId = p.Thread.Id
                })
                .ToListAsync();

            return Ok(posts);
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var post = await _db.ForumPosts
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (post == null)
                return NotFound();

            post.IsDeleted = true;
            post.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            return NoContent();
        }
    }
}
