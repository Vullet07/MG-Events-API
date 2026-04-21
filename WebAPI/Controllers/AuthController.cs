using Data;
using Data.Models;
using Microsoft.AspNetCore.Authorization;
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
using WebAPI.Services.Accounts;
using WebAPI.Services.Security;

namespace WebAPI.Controllers
{
    [Route("api/[controller]")]
    public class AuthController : ApiControllerBase
    {
        private static readonly TimeSpan EmailConfirmationLifetime = TimeSpan.FromHours(24);

        private readonly string _frontendBaseUrl;
        private readonly ITokenService _tokenService;
        private readonly IPasswordHasher<User> _passwordHasher;
        private readonly IAuthUserService _authUserService;
        private readonly IEmailService _emailService;
        private readonly IUserLifecycleService _userLifecycleService;
        private readonly ILoginAttemptQuarantineService _loginAttemptQuarantineService;
        private readonly AppDbContext _db;

        public AuthController(
            IConfiguration configuration,
            ITokenService tokenService,
            IPasswordHasher<User> passwordHasher,
            IAuthUserService authUserService,
            IEmailService emailService,
            IUserLifecycleService userLifecycleService,
            ILoginAttemptQuarantineService loginAttemptQuarantineService,
            AppDbContext db)
        {
            _frontendBaseUrl = (configuration["Frontend:BaseUrl"] ?? configuration["Backend:BaseUrl"] ?? "http://localhost:5173").TrimEnd('/');
            _tokenService = tokenService;
            _passwordHasher = passwordHasher;
            _authUserService = authUserService;
            _emailService = emailService;
            _userLifecycleService = userLifecycleService;
            _loginAttemptQuarantineService = loginAttemptQuarantineService;
            _db = db;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.Identifier) || string.IsNullOrWhiteSpace(dto.Password))
                return ToApiValidationFail("Missing login data");

            var remoteAddress = GetRemoteAddress();
            var quarantineState = _loginAttemptQuarantineService.GetState(remoteAddress);
            if (quarantineState.IsQuarantined)
            {
                return ToApiValidationFail(
                    $"Too many failed login attempts. Try again after {quarantineState.ExpiresAtUtc:yyyy-MM-dd HH:mm}.",
                    429);
            }

            var identifier = dto.Identifier.Trim();
            var normalizedIdentifier = NormalizeEmail(identifier);

            var user = await _db.Users
                .FirstOrDefaultAsync(u => u.Username == identifier || u.Email == normalizedIdentifier);

            if (user == null)
            {
                var failureState = _loginAttemptQuarantineService.RegisterFailure(remoteAddress);
                if (failureState.IsQuarantined)
                {
                    return ToApiValidationFail(
                        $"Too many failed login attempts. Try again after {failureState.ExpiresAtUtc:yyyy-MM-dd HH:mm}.",
                        429);
                }

                return ToApiValidationFail("Invalid credentials", 401);
            }

            if (user.ScheduledDeletionAt != null && user.ScheduledDeletionAt <= DateTime.UtcNow)
            {
                await _userLifecycleService.DeleteUserWithContentAsync(user.Id);
                return ToApiValidationFail("Student account expired and has been removed.", 403);
            }

            if (user.IsBanned)
            {
                if (user.BannedUntil == null || user.BannedUntil > DateTime.UtcNow)
                    return ToApiValidationFail("Account is banned", 403);

                user.IsBanned = false;
                user.BannedUntil = null;
                user.BanReason = null;
                await _db.SaveChangesAsync();
            }

            var passwordValid = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash ?? string.Empty, dto.Password);
            if (passwordValid == PasswordVerificationResult.Failed)
            {
                var failureState = _loginAttemptQuarantineService.RegisterFailure(remoteAddress);
                if (failureState.IsQuarantined)
                {
                    return ToApiValidationFail(
                        $"Too many failed login attempts. Try again after {failureState.ExpiresAtUtc:yyyy-MM-dd HH:mm}.",
                        429);
                }

                return ToApiValidationFail("Invalid credentials", 401);
            }

            if (!user.IsEmailConfirmed)
            {
                return ToApiValidationFail("Имейл адресът не е потвърден. Провери пощата си и активирай профила.", 403);
            }

            _loginAttemptQuarantineService.ClearFailures(remoteAddress);

            var tokenResult = _tokenService.GenerateToken(user.Id.ToString(), user.Role.ToString(), user.Username);

            var response = new LoginResponseDto
            {
                Token = tokenResult.Token,
                ExpiresAt = tokenResult.ExpiresAt,
                User = ToUserDto(user)
            };

            return ToApiValidationSuccess(response, "Login successful");
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] CreateUserDto dto)
        {
            if (!ModelState.IsValid)
                return ToApiValidationFail("Invalid registration data.");

            var username = dto.Username.Trim();
            var email = NormalizeEmail(dto.Email);

            if (await _db.Users.AnyAsync(u => u.Username == username))
                return ToApiValidationFail("Username already exists.", 409);

            if (await _db.Users.AnyAsync(u => u.Email == email))
                return ToApiValidationFail("Email already registered.");

            var confirmationToken = EmailConfirmationTokenHelper.GenerateToken();

            var user = new User
            {
                Username = username,
                Email = email,
                Role = Role.Student,
                PhotoUrl = dto.PhotoUrl,
                GradeLevel = dto.GradeLevel,
                SchoolYearStart = _userLifecycleService.DetermineSchoolYearStart(),
                ScheduledDeletionAt = _userLifecycleService.CalculateScheduledDeletionUtc(dto.GradeLevel),
                IsEmailConfirmed = false,
                EmailConfirmationTokenHash = EmailConfirmationTokenHelper.HashToken(confirmationToken),
                EmailConfirmationTokenExpiresAt = DateTime.UtcNow.Add(EmailConfirmationLifetime),
                EmailConfirmedAt = null
            };
            user.PasswordHash = _passwordHasher.HashPassword(user, dto.Password);

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            var confirmUrl = BuildConfirmEmailUrl("student", confirmationToken);
            await _emailService.SendAsync(
                user.Email,
                "Активирай профила си в MG Events",
                $"<p>Здравей, {user.Username}!</p><p>За да активираш ученическия си профил в MG Events за МГ &bdquo;Академик Кирил Попов&ldquo;, натисни <a href='{confirmUrl}'>този линк</a>.</p><p>Линкът е валиден 24 часа.</p>");

            return ToApiValidationSuccess("Регистрацията е записана. Провери имейла си и активирай профила, за да можеш да влезеш.");
        }

        [HttpPost("register-teacher-request")]
        [AllowAnonymous]
        public async Task<IActionResult> RegisterTeacherRequest([FromBody] CreateTeacherRegistrationRequestDto dto)
        {
            if (!ModelState.IsValid)
                return ToApiValidationFail("Invalid teacher registration data.");

            var username = dto.Username.Trim();
            var email = NormalizeEmail(dto.Email);

            if (await _db.Users.AnyAsync(u => u.Username == username))
                return ToApiValidationFail("Username already exists.", 409);

            if (await _db.Users.AnyAsync(u => u.Email == email))
                return ToApiValidationFail("Email already registered.", 409);

            var duplicatePending = await _db.TeacherRegistrationRequests.AnyAsync(r =>
                r.Status == TeacherRegistrationStatus.Pending &&
                (r.Username == username || r.Email == email));

            if (duplicatePending)
                return ToApiValidationFail("A pending teacher request with this username or email already exists.", 409);

            var teacherCandidate = new User
            {
                Username = username,
                Email = email,
                Role = Role.Teacher
            };

            var confirmationToken = EmailConfirmationTokenHelper.GenerateToken();

            var request = new TeacherRegistrationRequest
            {
                Username = username,
                Email = email,
                PasswordHash = _passwordHasher.HashPassword(teacherCandidate, dto.Password),
                Motivation = dto.Motivation?.Trim(),
                Status = TeacherRegistrationStatus.Pending,
                IsEmailConfirmed = false,
                EmailConfirmationTokenHash = EmailConfirmationTokenHelper.HashToken(confirmationToken),
                EmailConfirmationTokenExpiresAt = DateTime.UtcNow.Add(EmailConfirmationLifetime),
                EmailConfirmedAt = null
            };

            _db.TeacherRegistrationRequests.Add(request);
            await _db.SaveChangesAsync();

            var confirmUrl = BuildConfirmEmailUrl("teacher", confirmationToken);
            await _emailService.SendAsync(
                request.Email,
                "Потвърди учителската си заявка в MG Events",
                $"<p>Здравей, {request.Username}!</p><p>За да потвърдиш имейла към учителската си заявка в MG Events за МГ &bdquo;Академик Кирил Попов&ldquo;, отвори <a href='{confirmUrl}'>този линк</a>.</p><p>След потвърждение заявката ти ще чака преглед от администратор.</p>");

            return ToApiValidationSuccess("Заявката е записана. Потвърди имейла си, след което тя ще чака одобрение от администратор.");
        }

        [HttpPost("confirm-email")]
        [AllowAnonymous]
        public async Task<IActionResult> ConfirmEmail([FromBody] ConfirmEmailDto dto)
        {
            if (!ModelState.IsValid)
                return ToApiValidationFail("Missing confirmation data.");

            var kind = dto.Kind.Trim().ToLowerInvariant();
            var tokenHash = EmailConfirmationTokenHelper.HashToken(dto.Token.Trim());
            var now = DateTime.UtcNow;

            if (kind is "student" or "user")
            {
                var user = await _db.Users
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(u =>
                        !u.IsDeleted &&
                        !u.IsEmailConfirmed &&
                        u.EmailConfirmationTokenHash == tokenHash &&
                        u.EmailConfirmationTokenExpiresAt != null &&
                        u.EmailConfirmationTokenExpiresAt > now);

                if (user == null)
                    return ToApiValidationFail("Невалиден или изтекъл линк за потвърждение.", 400);

                user.IsEmailConfirmed = true;
                user.EmailConfirmedAt = now;
                user.EmailConfirmationTokenHash = null;
                user.EmailConfirmationTokenExpiresAt = null;
                await _db.SaveChangesAsync();

                return ToApiValidationSuccess("Имейлът е потвърден успешно. Вече можеш да влезеш в профила си.");
            }

            if (kind == "teacher")
            {
                var request = await _db.TeacherRegistrationRequests
                    .FirstOrDefaultAsync(r =>
                        r.Status == TeacherRegistrationStatus.Pending &&
                        !r.IsEmailConfirmed &&
                        r.EmailConfirmationTokenHash == tokenHash &&
                        r.EmailConfirmationTokenExpiresAt != null &&
                        r.EmailConfirmationTokenExpiresAt > now);

                if (request == null)
                    return ToApiValidationFail("Невалиден или изтекъл линк за потвърждение.", 400);

                request.IsEmailConfirmed = true;
                request.EmailConfirmedAt = now;
                request.EmailConfirmationTokenHash = null;
                request.EmailConfirmationTokenExpiresAt = null;
                await _db.SaveChangesAsync();

                return ToApiValidationSuccess("Имейлът е потвърден успешно. Учителската заявка вече очаква преглед от администратор.");
            }

            return ToApiValidationFail("Невалиден тип за потвърждение.", 400);
        }

        [HttpPost("forgot-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordDto dto)
        {
            var normalizedEmail = NormalizeEmail(dto.Email);
            var user = await _db.Users
                .FirstOrDefaultAsync(u =>
                    u.Email == normalizedEmail &&
                    !u.IsDeleted &&
                    !u.IsBanned &&
                    u.IsEmailConfirmed);

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
                $"{_frontendBaseUrl}/reset-password?token={Uri.EscapeDataString(token)}";

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

        [Authorize]
        [HttpGet("me")]
        public IActionResult Me()
        {
            if (!_authUserService.IsAuthenticated || _authUserService.Id == null)
                return ToApiValidationFail("User is not authenticated.", 401);

            var user = _db.Users.Find(_authUserService.Id.Value);
            if (user == null)
                return ToApiValidationFail("User not found.", 404);

            var userDto = ToUserDto(user);
            userDto.ThreadsCount = _db.ForumThreads.Count(t => t.CreatedByUser.Id == user.Id);
            userDto.PostsCount = _db.ForumPosts.Count(p => p.User.Id == user.Id && !p.IsDeleted);
            userDto.PinsCount = _db.EventPins.Count(p => p.CreatedByUser.Id == user.Id && !p.IsResolved);

            return ToApiValidationSuccess(userDto);
        }

        private string? GetRemoteAddress()
        {
            var forwardedFor = HttpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(forwardedFor))
            {
                return forwardedFor.Split(',').FirstOrDefault()?.Trim();
            }

            return HttpContext.Connection.RemoteIpAddress?.ToString();
        }

        private string BuildConfirmEmailUrl(string kind, string token)
        {
            return $"{_frontendBaseUrl}/confirm-email?kind={Uri.EscapeDataString(kind)}&token={Uri.EscapeDataString(token)}";
        }

        private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

        private static UserDto ToUserDto(User user)
        {
            return new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                Role = user.Role,
                IsEmailConfirmed = user.IsEmailConfirmed,
                PhotoUrl = user.PhotoUrl,
                IsBanned = user.IsBanned && (user.BannedUntil == null || user.BannedUntil > DateTime.UtcNow),
                BannedUntil = user.BannedUntil,
                GradeLevel = user.GradeLevel,
                SchoolYearStart = user.SchoolYearStart,
                ScheduledDeletionAt = user.ScheduledDeletionAt
            };
        }
    }
}

