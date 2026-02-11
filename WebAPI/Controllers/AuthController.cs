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
using Services.PasswordResetService;
using Services.PasswordResetService.EmailService;
using System.Linq;
using WebAPI.Extensions;

namespace WebAPI.Controllers
{
    [Route("api/[controller]")]
    public class AuthController : ApiControllerBase
    {
        private readonly string _backendBaseUrl;
        private readonly ITokenService _tokenService;
        private readonly IPasswordHasher<User> _passwordHasher;
        private readonly IAuthUserService _authUserService;
        private readonly IEmailService _emailService;
        private readonly AppDbContext _db;

        public AuthController(IConfiguration configuration, ITokenService tokenService, IPasswordHasher<User> passwordHasher, IAuthUserService authUserService, IEmailService emailService, AppDbContext db)
        {
            _backendBaseUrl = configuration["Backend:BaseUrl"]!;
            _tokenService = tokenService;
            _passwordHasher = passwordHasher;
            _authUserService = authUserService;
            _emailService = emailService;
            _db = db;
        }



        // ---------------- LOGIN ----------------
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.Identifier) || string.IsNullOrWhiteSpace(dto.Password))
                return ToApiValidationFail("Missing login data");

            // Find user by username OR email
            var user = await _db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Username == dto.Identifier || u.Email == dto.Identifier);

            if (user == null)
                return ToApiValidationFail("Invalid credentials", 401);

            // Check if user is banned
            if (user.IsBanned)
            {
                if (user.BannedUntil == null || user.BannedUntil > DateTime.UtcNow)
                    return ToApiValidationFail("Account is banned", 403);

                // Lift ban if expired
                user.IsBanned = false;
                user.BannedUntil = null;
                user.BanReason = null;
                await _db.SaveChangesAsync();
            }

            // Verify password
            var passwordValid = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, dto.Password);
            if (passwordValid == PasswordVerificationResult.Failed)
                return ToApiValidationFail("Invalid credentials", 401);

            // Generate JWT
            var tokenResult = _tokenService.GenerateToken(user.Id.ToString(), user.Role.ToString(), user.Username);

            var response = new LoginResponseDto
            {
                Token = tokenResult.Token,
                ExpiresAt = tokenResult.ExpiresAt,
                User = new UserDto
                {
                    Id = user.Id,
                    Username = user.Username,
                    Email = user.Email,
                    Role = user.Role,
                    PhotoUrl = user.PhotoUrl
                }
            };

            return ToApiValidationSuccess(response, "Login successful");
        }


        // ---------------- REGISTER ----------------
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] CreateUserDto dto)
        {
            if (!ModelState.IsValid)
                return ToApiValidationFail("Invalid registration data.");

            if (await _db.Users.AnyAsync(u => u.Username == dto.Username))
                return ToApiValidationFail("Username already exists.", 409);

            if (await _db.Users.AnyAsync(u => u.Email == dto.Email))
                return ToApiValidationFail("Email already registered.");

            var user = new User
            {
                Username = dto.Username,
                Email = dto.Email,
                Role = Role.Student,
                PhotoUrl = dto.PhotoUrl
            };
            user.PasswordHash = _passwordHasher.HashPassword(user, dto.Password);

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

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
                    Email = user.Email,
                    Role = user.Role,
                    PhotoUrl = user.PhotoUrl
                }
            };

            return ToApiValidationSuccess(response, "User registered successfully.");
        }

        [HttpPost("forgot-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordDto dto)
        {
            var user = await _db.Users
                .FirstOrDefaultAsync(u =>
                    u.Email == dto.Email &&
                    !u.IsDeleted &&
                    !u.IsBanned);

            // Prevent user enumeration
            if (user == null)
                return ToApiValidationSuccess("If the email exists, a reset link was sent.");

            var token = PasswordResetTokenHelper.GenerateToken();

            var resetToken = new PasswordResetToken
            {
                User = user,
                TokenHash = PasswordResetTokenHelper.HashToken(token),
                ExpiresAt = DateTime.UtcNow.AddMinutes(30)
            };

            _db.PasswordResetTokens.Add(resetToken);
            await _db.SaveChangesAsync();

            var resetUrl =
                $"{_backendBaseUrl}/reset-password?token={Uri.EscapeDataString(token)}";

            await _emailService.SendAsync(
                user.Email,
                "Reset your password",
                $"Click <a href='{resetUrl}'>here</a> to reset your password."
            );

            return ToApiValidationSuccess("If the email exists, a reset link was sent.");
        }

        [HttpPost("reset-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ResetPassword(ResetPasswordDto dto)
        {
            var tokenHash = PasswordResetTokenHelper.HashToken(dto.Token);

            var resetToken = await _db.PasswordResetTokens
                .Include(x => x.User)
                .FirstOrDefaultAsync(x =>
                    x.TokenHash == tokenHash &&
                    !x.IsUsed &&
                    x.ExpiresAt > DateTime.UtcNow &&
                    !x.User.IsDeleted &&
                    !x.User.IsBanned);

            if (resetToken == null)
                return ToApiValidationFail("Invalid or expired reset token.", 400);

            resetToken.User.PasswordHash =
                _passwordHasher.HashPassword(resetToken.User, dto.NewPassword);

            resetToken.IsUsed = true;

            await _db.SaveChangesAsync();

            return ToApiValidationSuccess("Password reset successfully.");
        }

        // ---------------- ME ----------------
        [Authorize]
        [HttpGet("me")]
        public IActionResult Me()
        {
            if (!_authUserService.IsAuthenticated || _authUserService.Id == null)
                return ToApiValidationFail("User is not authenticated.", 401);

            var user = _db.Users.Find(_authUserService.Id.Value);
            if (user == null)
                return ToApiValidationFail("User not found.", 404);

            var userDto = new UserDto
            {
                Id = user.Id,
                Email = user.Email,
                Username = user.Username,
                Role = user.Role,
                PhotoUrl = user.PhotoUrl,
                ThreadsCount = _db.ForumThreads.Count(t => t.CreatedByUser.Id == user.Id),
                PostsCount = _db.ForumPosts.Count(p => p.User.Id == user.Id && !p.IsDeleted),
                PinsCount = _db.EventPins.Count(p => p.CreatedByUser.Id == user.Id)
            };

            return ToApiValidationSuccess(userDto);
        }
    }
}
