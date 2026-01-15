using Data;
using Data.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Services.AuthUserService;
using Services.Dtos;
using WebAPI.Extensions;

namespace WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class UserController : ApiControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IPasswordHasher<User> _passwordHasher;
        private readonly ILogger<UserController> _logger;
        private readonly IAuthUserService _authUser;

        public UserController(AppDbContext db, IPasswordHasher<User> passwordHasher, ILogger<UserController> logger, IAuthUserService authUser)
        {
            _db = db;
            _passwordHasher = passwordHasher;
            _logger = logger;
            _authUser = authUser;
        }


        // ---------------- GET ALL ----------------
        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] PagingQuery paging)
        {
            var query = _db.Users.AsQueryable();

            var totalCount = await query.CountAsync();

            var users = await query
                .Skip(paging.Skip)
                .Take(paging.PageSize)
                .Select(u => new UserDto
                {
                    Id = u.Id,
                    Username = u.Username,
                    Role = u.Role,
                    PhotoUrl = u.PhotoUrl
                })
                .ToListAsync();

            var response = new PagedResponse<UserDto>
            {
                Items = users,
                Page = paging.Page,
                PageSize = paging.PageSize,
                TotalCount = totalCount
            };

            return ToApiValidationSuccess(response);
        }

        // ---------------- GET BY ID ----------------
        [HttpGet("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetById(int id)
        {
            var user = await _db.Users.FindAsync(id);
            if (user == null)
                return ToApiValidationFail("User not found.", 404);

            var userDto = new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                Role = user.Role,
                PhotoUrl = user.PhotoUrl
            };

            return ToApiValidationSuccess(userDto);
        }

        // ---------------- UPDATE USER ----------------
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateUserDto dto)
        {
            var user = await _db.Users.FindAsync(id);
            if (user == null)
                return ToApiValidationFail("User not found.", 404);

            if (_authUser.Role != Role.Admin && user.Id != _authUser.Id)
                return ToApiValidationFail("Only admins can update other users' info", 400);

            if (!string.IsNullOrEmpty(dto.Username))
                user.Username = dto.Username;

            if (!string.IsNullOrEmpty(dto.PhotoUrl))
                user.PhotoUrl = dto.PhotoUrl;

            if (dto.Role.HasValue)
                user.Role = dto.Role.Value;

            if (!string.IsNullOrEmpty(dto.Password))
                user.PasswordHash = _passwordHasher.HashPassword(user, dto.Password);

            await _db.SaveChangesAsync();

            var userDto = new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                Role = user.Role,
                PhotoUrl = user.PhotoUrl
            };

            return ToApiValidationSuccess(userDto, "User updated successfully.");
        }

        // ---------------- DELETE USER ----------------
        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var user = await _db.Users.FindAsync(id);
            if (user == null)
                return ToApiValidationFail("User not found.", 404);

            user.IsDeleted = true;
            user.DeletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return ToApiValidationSuccess("User deleted successfully.");
        }
    }
}
