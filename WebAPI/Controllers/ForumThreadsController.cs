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
    [ApiController]
    [Route("api/forum-threads")]
    public class ForumThreadsController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IAuthUserService _authUser;

        public ForumThreadsController(AppDbContext db, IAuthUserService authUser)
        {
            _db = db;
            _authUser = authUser;
        }

        // ---------------- Create thread ----------------
        [Authorize]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateForumThreadDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var user = await _db.Users.FindAsync(_authUser.Id);
            if (user == null)
                return Unauthorized();

            var thread = new ForumThread
            {
                Title = dto.Title,
                CreatedByUser = user,
                CreatedAt = DateTime.UtcNow,
                IsLocked = false,
                IsPinned = false
            };

            _db.ForumThreads.Add(thread);
            await _db.SaveChangesAsync();

            return Ok(ToDto(thread));
        }

        // ---------------- Get all threads ----------------
        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var threads = await _db.ForumThreads
                .OrderByDescending(t => t.IsPinned)
                .ThenByDescending(t => t.LastPostAt ?? t.CreatedAt)
                .Select(t => new ForumThreadDto
                {
                    Id = t.Id,
                    Title = t.Title,
                    IsLocked = t.IsLocked,
                    IsPinned = t.IsPinned,
                    CreatedAt = t.CreatedAt,
                    LastPostAt = t.LastPostAt,
                    CreatedByUserId = t.CreatedByUser.Id
                })
                .ToListAsync();

            return Ok(threads);
        }

        // ---------------- Get single thread ----------------
        [AllowAnonymous]
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var thread = await _db.ForumThreads
                .Include(t => t.CreatedByUser)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (thread == null)
                return NotFound();

            return Ok(ToDto(thread));
        }

        // ---------------- Lock / unlock (Boss only) ----------------
        [Authorize]
        [HttpPut("{id:int}/lock")]
        public async Task<IActionResult> Lock(int id)
        {
            var thread = await _db.ForumThreads.FindAsync(id);
            if (thread == null)
                return NotFound();

            thread.IsLocked = true;
            await _db.SaveChangesAsync();

            return NoContent();
        }

        [Authorize]
        [HttpPut("{id:int}/unlock")]
        public async Task<IActionResult> Unlock(int id)
        {
            var thread = await _db.ForumThreads.FindAsync(id);
            if (thread == null)
                return NotFound();

            thread.IsLocked = false;
            await _db.SaveChangesAsync();

            return NoContent();
        }

        // ---------------- Pin / unpin (Boss only) ----------------
        [Authorize]
        [HttpPut("{id:int}/pin")]
        public async Task<IActionResult> Pin(int id)
        {
            var thread = await _db.ForumThreads.FindAsync(id);
            if (thread == null)
                return NotFound();

            thread.IsPinned = true;
            await _db.SaveChangesAsync();

            return NoContent();
        }

        [Authorize]
        [HttpPut("{id:int}/unpin")]
        public async Task<IActionResult> Unpin(int id)
        {
            var thread = await _db.ForumThreads.FindAsync(id);
            if (thread == null)
                return NotFound();

            thread.IsPinned = false;
            await _db.SaveChangesAsync();

            return NoContent();
        }

        // ---------------- Helper ----------------
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
                CreatedByUserId = thread.CreatedByUser.Id
            };
        }
    }
}
