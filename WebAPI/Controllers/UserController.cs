using Data;
using Data.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Services.Dtos;

namespace WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class UserController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IPasswordHasher<User> _passwordHasher;
        private readonly ILogger<UserController> _logger;

        public UserController(
            AppDbContext db,
            IPasswordHasher<User> passwordHasher,
            ILogger<UserController> logger)
        {
            _db = db;
            _passwordHasher = passwordHasher;
            _logger = logger;
        }

        // ---------------- CREATE USER ----------------

        

        // ---------------- GET ALL ----------------

        [HttpGet]
        public async Task<ActionResult<List<UserDto>>> GetAll()
        {
            var users = await _db.Users
                .Select(u => new UserDto
                {
                    Id = u.Id,
                    Username = u.Username,
                    Role = u.Role,
                    PhotoUrl = u.PhotoUrl
                })
                .ToListAsync();

            return Ok(users);
        }

        // ---------------- GET BY ID ----------------

        [HttpGet("{id:int}")]
        public async Task<ActionResult<UserDto>> GetById(int id)
        {
            var user = await _db.Users.FindAsync(id);
            if (user == null)
                return NotFound("User not found.");

            return Ok(new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                Role = user.Role,
                PhotoUrl = user.PhotoUrl
            });
        }

        // ---------------- UPDATE USER ----------------

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateUserDto dto)
        {
            var user = await _db.Users.FindAsync(id);
            if (user == null)
                return NotFound("User not found.");

            if (dto.Username != null)
                user.Username = dto.Username;

            if (dto.PhotoUrl != null)
                user.PhotoUrl = dto.PhotoUrl;

            if (dto.Role.HasValue)
                user.Role = dto.Role.Value;

            if (dto.Password != null)
                user.PasswordHash = _passwordHasher.HashPassword(user, dto.Password);

            await _db.SaveChangesAsync();

            return Ok(new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                Role = user.Role,
                PhotoUrl = user.PhotoUrl
            });
        }

        // ---------------- DELETE USER ----------------

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var user = await _db.Users.FindAsync(id);
            if (user == null)
                return NotFound("User not found.");

            _db.Users.Remove(user);
            await _db.SaveChangesAsync();

            return NoContent();
        }
    }
}
