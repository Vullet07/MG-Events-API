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
        private readonly IAuthUserService _authUser;
        private readonly IWebHostEnvironment _env;

        public UserController(AppDbContext db, IPasswordHasher<User> passwordHasher, IAuthUserService authUser, IWebHostEnvironment env)
        {
            _db = db;
            _passwordHasher = passwordHasher;
            _authUser = authUser;
            _env = env;
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
                    Email = u.Email,
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
        [Authorize]
        public async Task<IActionResult> GetById(int id)
        {
            var user = await _db.Users.FindAsync(id);
            if (user == null)
                return ToApiValidationFail("User not found.", 404);

            var userDto = new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                Role = user.Role,
                PhotoUrl = user.PhotoUrl
            };

            return ToApiValidationSuccess(userDto);
        }

        // ---------------- GET PUBLIC BY ID ----------------
        [HttpGet("public/{id:int}")]
        [Authorize]
        public async Task<IActionResult> GetPublicById(int id)
        {
            var user = await _db.Users.FindAsync(id);
            if (user == null)
                return ToApiValidationFail("User not found.", 404);

            var userDto = new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
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

            if (!string.IsNullOrEmpty(dto.Email))
            {
                if (await _db.Users.AnyAsync(u => u.Email == dto.Email && u.Id != id))
                    return ToApiValidationFail("Email is already used by another user.", 400);

                user.Email = dto.Email;
            }

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
                Email = user.Email,
                Role = user.Role,
                PhotoUrl = user.PhotoUrl
            };

            return ToApiValidationSuccess(userDto, "User updated successfully.");
        }

        // ---------------- UPLOAD PROFILE PHOTO ----------------
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
            user.PhotoUrl = $"{baseUrl}/{relativePath.Replace("\\\\", "/")}/{fileName}";
            await _db.SaveChangesAsync();

            var userDto = new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                Role = user.Role,
                PhotoUrl = user.PhotoUrl
            };

            return ToApiValidationSuccess(userDto, "Profile photo updated.");
        }

        [Authorize(Roles = "Admin,Teacher")]
        [HttpPost("{id:int}/ban")]
        public async Task<IActionResult> BanUser(int id, [FromBody] BanUserDto dto)
        {
            var user = await _db.Users.FindAsync(id);
            if (user == null)
                return ToApiValidationFail("User not found.", 404);

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
