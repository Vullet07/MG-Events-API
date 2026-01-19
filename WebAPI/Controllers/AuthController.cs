using Data;
using Data.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Services.AuthUserService;
using Services.Dtos;
using Services.JwtService;
using System.Linq;
using WebAPI.Extensions;

namespace WebAPI.Controllers
{
    [Route("api/[controller]")]
    public class AuthController : ApiControllerBase
    {
        private readonly ITokenService _tokenService;
        private readonly IPasswordHasher<User> _passwordHasher;
        private readonly IAuthUserService _authUserService;
        private readonly AppDbContext _db;

        public AuthController(
            ITokenService tokenService,
            IPasswordHasher<User> passwordHasher,
            IAuthUserService authUserService,
            AppDbContext db)
        {
            _tokenService = tokenService;
            _passwordHasher = passwordHasher;
            _authUserService = authUserService;
            _db = db;
        }

        // ---------------- LOGIN ----------------
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            if (dto == null)
                return ToApiValidationFail("Missing login data.");

            var user = await _db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Username == dto.Username);

            if (user == null)
                return ToApiValidationFail("Invalid username or password.", 401);

            if (user.IsBanned)
            {
                if (user.BannedUntil == null || user.BannedUntil > DateTime.UtcNow)
                    return ToApiValidationFail("Account is banned", 403);

                user.IsBanned = false;
                user.BannedUntil = null;
                user.BanReason = null;
                await _db.SaveChangesAsync();
            }

            var passwordValid = _passwordHasher.VerifyHashedPassword(
                user,
                user.PasswordHash,
                dto.Password);

            if (passwordValid == PasswordVerificationResult.Failed)
                return ToApiValidationFail("Invalid username or password.", 401);

            var tokenResult = _tokenService.GenerateToken(
                user.Id.ToString(),
                user.Role.ToString(),
                user.Username);

            var response = new LoginResponseDto
            {
                Token = tokenResult.Token,
                ExpiresAt = tokenResult.ExpiresAt,
                User = new UserDto
                {
                    Id = user.Id,
                    Username = user.Username,
                    Role = user.Role,
                    PhotoUrl = user.PhotoUrl
                }
            };

            return ToApiValidationSuccess(response, "Login successful.");
        }

        // ---------------- REGISTER ----------------
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] CreateUserDto dto)
        {
            if (!ModelState.IsValid)
                return ToApiValidationFail("Invalid registration data.");

            if (await _db.Users.AnyAsync(u => u.Username == dto.Username))
                return ToApiValidationFail("Username already exists.", 409);

            var user = new User
            {
                Username = dto.Username,
                Role = Role.Student,
                PhotoUrl = dto.PhotoUrl
            };
            user.PasswordHash = _passwordHasher.HashPassword(user, dto.Password);

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            // Generate token AFTER saving user
            var tokenResult = _tokenService.GenerateToken(
                user.Id.ToString(),
                user.Role.ToString(),
                user.Username);

            var response = new LoginResponseDto
            {
                Token = tokenResult.Token,
                ExpiresAt = tokenResult.ExpiresAt,
                TokenType = "Bearer",
                User = new UserDto
                {
                    Id = user.Id,
                    Username = user.Username,
                    Role = user.Role,
                    PhotoUrl = user.PhotoUrl
                }
            };

            return ToApiValidationSuccess(response, "User registered successfully.");
        }

        // ---------------- ME ----------------
        [Authorize]
        [HttpGet("me")]
        public IActionResult Me()
        {
            if (!_authUserService.IsAuthenticated || _authUserService.Id == null)
                return ToApiValidationFail("User is not authenticated.", 401);

            var userDto = new UserDto
            {
                Id = _authUserService.Id.Value,
                Username = _authUserService.Username!,
                Role = _authUserService.Role ?? Role.Student,
                PhotoUrl = null
            };

            return ToApiValidationSuccess(userDto);
        }
    }
}
