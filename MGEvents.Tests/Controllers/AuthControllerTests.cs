using Data;
using Data.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Services.AuthUserService;
using Services.Dtos;
using Services.JwtService;
using Services.PasswordResetService;
using Services.PasswordResetService.EmailService;
using WebAPI.Controllers;
using WebAPI.Extensions;
using WebAPI.Services.Accounts;
using WebAPI.Services.Security;

namespace MGEvents.Tests.Controllers;

public class AuthControllerTests
{
    [Fact]
    public async Task Login_ReturnsFailure_WhenEmailIsNotConfirmed()
    {
        await using var db = CreateDbContext();
        var passwordHasher = new PasswordHasher<User>();
        var user = new User
        {
            Username = "student-login",
            Email = "student-login@schoolmath.eu",
            PasswordHash = string.Empty,
            Role = Role.Student,
            IsEmailConfirmed = false
        };
        user.PasswordHash = passwordHasher.HashPassword(user, "Test123!");

        db.Users.Add(user);
        await db.SaveChangesAsync();

        var controller = CreateController(db, passwordHasher);

        var result = await controller.Login(new LoginDto
        {
            Identifier = user.Email,
            Password = "Test123!"
        });

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<ApiResponse>(ok.Value);
        Assert.False(payload.Success);
        Assert.Contains("\u043f\u043e\u0442\u0432\u044a\u0440\u0434\u0435\u043d", payload.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ConfirmEmail_ActivatesPendingStudentAccount()
    {
        await using var db = CreateDbContext();
        var rawToken = EmailConfirmationTokenHelper.GenerateToken();
        var user = new User
        {
            Username = "pending-student",
            Email = "pending-student@schoolmath.eu",
            PasswordHash = "hash",
            Role = Role.Student,
            IsEmailConfirmed = false,
            EmailConfirmationTokenHash = EmailConfirmationTokenHelper.HashToken(rawToken),
            EmailConfirmationTokenExpiresAt = DateTime.UtcNow.AddHours(6)
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        var controller = CreateController(db);

        var result = await controller.ConfirmEmail(new ConfirmEmailDto
        {
            Kind = "student",
            Token = rawToken
        });

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<ApiResponse>(ok.Value);
        Assert.True(payload.Success);

        var updated = await db.Users.IgnoreQueryFilters().SingleAsync(item => item.Id == user.Id);
        Assert.True(updated.IsEmailConfirmed);
        Assert.NotNull(updated.EmailConfirmedAt);
        Assert.Null(updated.EmailConfirmationTokenHash);
        Assert.Null(updated.EmailConfirmationTokenExpiresAt);
    }

    [Fact]
    public async Task Register_CreatesInactiveStudentAndSendsConfirmationEmail()
    {
        await using var db = CreateDbContext();
        var passwordHasher = new PasswordHasher<User>();
        var emailService = new RecordingEmailService();
        var controller = CreateController(db, passwordHasher, emailService);

        var result = await controller.Register(new CreateUserDto
        {
            Username = "new-student",
            Email = "new-student@schoolmath.eu",
            Password = "Test123!",
            GradeLevel = 11
        });

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<ApiResponse>(ok.Value);
        Assert.True(payload.Success);

        var user = await db.Users.IgnoreQueryFilters().SingleAsync(item => item.Username == "new-student");
        Assert.False(user.IsEmailConfirmed);
        Assert.NotNull(user.EmailConfirmationTokenHash);
        Assert.NotNull(user.EmailConfirmationTokenExpiresAt);
        Assert.Single(emailService.Messages);
        Assert.Contains("confirm-email", emailService.Messages[0].HtmlBody, StringComparison.OrdinalIgnoreCase);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new AppDbContext(options);
    }

    private static AuthController CreateController(
        AppDbContext db,
        IPasswordHasher<User>? passwordHasher = null,
        RecordingEmailService? emailService = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Backend:BaseUrl"] = "http://localhost:5173"
            })
            .Build();

        var controller = new AuthController(
            configuration,
            new TestTokenService(),
            passwordHasher ?? new PasswordHasher<User>(),
            new TestAuthUserService(),
            emailService ?? new RecordingEmailService(),
            new StubUserLifecycleService(),
            new LoginAttemptQuarantineService(new MemoryCache(new MemoryCacheOptions())),
            db);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        return controller;
    }

    private sealed class TestTokenService : ITokenService
    {
        public TokenResult GenerateToken(string userId, string role, string userName)
            => new() { Token = "token", ExpiresAt = DateTime.UtcNow.AddHours(1) };
    }

    private sealed class TestAuthUserService : IAuthUserService
    {
        public int? Id => null;
        public string? Email => null;
        public string? Username => null;
        public Role? Role => null;
        public bool IsAuthenticated => false;
        public bool IsBanned => false;
        public DateTime? BannedUntil => null;
    }

    private sealed class RecordingEmailService : IEmailService
    {
        public List<(string To, string Subject, string HtmlBody)> Messages { get; } = [];

        public Task SendAsync(string to, string subject, string htmlBody)
        {
            Messages.Add((to, subject, htmlBody));
            return Task.CompletedTask;
        }
    }

    private sealed class StubUserLifecycleService : IUserLifecycleService
    {
        public int DetermineSchoolYearStart(DateTime? referenceUtc = null) => 2026;

        public DateTime CalculateScheduledDeletionUtc(int gradeLevel, DateTime? referenceUtc = null)
            => new DateTime(2027, 7, 1, 0, 0, 0, DateTimeKind.Utc);

        public Task<bool> DeleteIfExpiredAsync(int userId, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<int> DeleteExpiredUsersAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<UserDeletionSummary?> DeleteUserWithContentAsync(int userId, CancellationToken cancellationToken = default)
            => Task.FromResult<UserDeletionSummary?>(null);
    }
}
