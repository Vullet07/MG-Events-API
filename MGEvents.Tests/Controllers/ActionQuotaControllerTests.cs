using Data;
using Data.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Services.AuthUserService;
using Services.Dtos;
using Services.Maps;
using WebAPI.Controllers;
using WebAPI.Extensions;
using WebAPI.Models;
using WebAPI.Services.Accounts;
using WebAPI.Services.Quotas;

namespace MGEvents.Tests.Controllers;

public class ActionQuotaControllerTests
{
    [Fact]
    public async Task EventPinsCreate_WhenHourlyLimitReached_Returns429()
    {
        await using var db = CreateDbContext();
        var user = CreateUser("pin-quota-user");
        db.Users.Add(user);
        await db.SaveChangesAsync();
        AddQuotaEvents(db, user, UserActionQuotaType.EventPinCreate, 5);
        await db.SaveChangesAsync();

        var controller = WithHttpContext(new EventPinsController(
            db,
            new TestAuthUserService(user),
            new TestWebHostEnvironment(),
            new UserActionQuotaService(db)));

        var point = IndoorMapGeometry.EncodeLayerPoint("campus", 600d, 250d);
        var result = await controller.Create(new CreateEventPinForm
        {
            Title = "Тестов пин",
            Category = "Поддръжка",
            Latitude = point.Latitude,
            Longitude = point.Longitude
        });

        AssertRateLimited(result);
    }

    [Fact]
    public async Task ForumPostsCreate_WhenHourlyLimitReached_Returns429()
    {
        await using var db = CreateDbContext();
        var user = CreateUser("post-quota-user");
        var thread = new ForumThread
        {
            Title = "Тема за квота",
            CreatedByUser = user,
            CreatedAt = DateTime.UtcNow
        };

        db.Users.Add(user);
        db.ForumThreads.Add(thread);
        await db.SaveChangesAsync();
        AddQuotaEvents(db, user, UserActionQuotaType.ForumPostCreate, 20);
        await db.SaveChangesAsync();

        var controller = WithHttpContext(new ForumPostsController(
            db,
            new TestAuthUserService(user),
            new TestWebHostEnvironment(),
            new UserActionQuotaService(db)));

        var result = await controller.Create(new CreateForumPostForm
        {
            ThreadId = thread.Id,
            Title = "Отговор",
            Content = "Съдържание"
        });

        AssertRateLimited(result);
    }

    [Fact]
    public async Task ForumThreadsCreate_WhenDailyLimitReached_Returns429()
    {
        await using var db = CreateDbContext();
        var user = CreateUser("thread-quota-user");
        db.Users.Add(user);
        await db.SaveChangesAsync();
        AddQuotaEvents(db, user, UserActionQuotaType.ForumThreadCreate, 1);
        await db.SaveChangesAsync();

        var controller = WithHttpContext(new ForumThreadsController(
            db,
            new TestAuthUserService(user),
            new TestWebHostEnvironment(),
            new UserActionQuotaService(db)));

        var result = await controller.Create(new CreateForumThreadDto
        {
            Title = "Нова тема"
        });

        AssertRateLimited(result);
    }

    [Fact]
    public async Task ReportsCreate_WhenDailyLimitReached_Returns429()
    {
        await using var db = CreateDbContext();
        var reporter = CreateUser("report-quota-user");
        var target = CreateUser("reported-user");
        db.Users.AddRange(reporter, target);
        await db.SaveChangesAsync();
        AddQuotaEvents(db, reporter, UserActionQuotaType.ReportCreate, 3);
        await db.SaveChangesAsync();

        var controller = WithHttpContext(new ReportsController(
            db,
            new TestAuthUserService(reporter),
            new TestUserLifecycleService(),
            new UserActionQuotaService(db)));

        var result = await controller.Create(new CreateReportDto
        {
            TargetType = ReportTargetType.User,
            TargetId = target.Id,
            Reason = "Спам"
        });

        AssertRateLimited(result);
    }

    [Fact]
    public async Task ReportsCreate_WhenPostAlreadyReportedBySameUser_Returns409()
    {
        await using var db = CreateDbContext();
        var reporter = CreateUser("duplicate-report-user");
        var thread = new ForumThread
        {
            Title = "Thread with reported post",
            CreatedByUser = reporter,
            CreatedAt = DateTime.UtcNow
        };
        var post = new ForumPost
        {
            Title = "Reported post",
            Content = "Reported content",
            User = reporter,
            Thread = thread,
            CreatedAt = DateTime.UtcNow
        };

        db.Users.Add(reporter);
        db.ForumThreads.Add(thread);
        db.ForumPosts.Add(post);
        await db.SaveChangesAsync();

        db.Reports.Add(new Report
        {
            Reporter = reporter,
            TargetType = ReportTargetType.Post,
            TargetId = post.Id,
            Reason = "Duplicate"
        });
        await db.SaveChangesAsync();

        var controller = WithHttpContext(new ReportsController(
            db,
            new TestAuthUserService(reporter),
            new TestUserLifecycleService(),
            new UserActionQuotaService(db)));

        var result = await controller.Create(new CreateReportDto
        {
            TargetType = ReportTargetType.Post,
            TargetId = post.Id,
            Reason = "Duplicate again"
        });

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<ApiResponse>(ok.Value);
        Assert.False(payload.Success);
        Assert.Contains("Вече", payload.Message);
        Assert.Equal(0, await db.UserActionQuotaEvents.CountAsync());
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new AppDbContext(options);
    }

    private static TController WithHttpContext<TController>(TController controller)
        where TController : ControllerBase
    {
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        controller.ControllerContext.HttpContext.Request.Scheme = "https";
        controller.ControllerContext.HttpContext.Request.Host = new HostString("localhost:7277");
        return controller;
    }

    private static User CreateUser(string username, Role role = Role.Student)
    {
        return new User
        {
            Username = username,
            Email = $"{username}@schoolmath.eu",
            PasswordHash = "hash",
            Role = role,
            IsEmailConfirmed = true
        };
    }

    private static void AddQuotaEvents(
        AppDbContext db,
        User user,
        UserActionQuotaType actionType,
        int count)
    {
        for (var i = 0; i < count; i++)
        {
            db.UserActionQuotaEvents.Add(new UserActionQuotaEvent
            {
                UserId = user.Id,
                User = user,
                ActionType = actionType,
                CreatedAt = DateTime.UtcNow.AddMinutes(-10 + i)
            });
        }
    }

    private static void AssertRateLimited(IActionResult result)
    {
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status429TooManyRequests, objectResult.StatusCode);
        var payload = Assert.IsType<ApiResponse>(objectResult.Value);
        Assert.False(payload.Success);
        Assert.Contains("Достигна лимита", payload.Message);
    }

    private sealed class TestAuthUserService : IAuthUserService
    {
        private readonly User _user;

        public TestAuthUserService(User user)
        {
            _user = user;
        }

        public int? Id => _user.Id;
        public string? Email => _user.Email;
        public string? Username => _user.Username;
        public Role? Role => _user.Role;
        public bool IsAuthenticated => true;
        public bool IsBanned => false;
        public DateTime? BannedUntil => null;
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public TestWebHostEnvironment()
        {
            var rootPath = Path.Combine(Path.GetTempPath(), "mg-events-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(rootPath);
            ApplicationName = "MGEvents.Tests";
            EnvironmentName = "Development";
            ContentRootPath = rootPath;
            WebRootPath = rootPath;
            ContentRootFileProvider = new NullFileProvider();
            WebRootFileProvider = new NullFileProvider();
        }

        public string ApplicationName { get; set; }
        public IFileProvider WebRootFileProvider { get; set; }
        public string WebRootPath { get; set; }
        public string EnvironmentName { get; set; }
        public string ContentRootPath { get; set; }
        public IFileProvider ContentRootFileProvider { get; set; }
    }

    private sealed class TestUserLifecycleService : IUserLifecycleService
    {
        public int DetermineSchoolYearStart(DateTime? referenceUtc = null) => DateTime.UtcNow.Year;

        public DateTime CalculateScheduledDeletionUtc(int gradeLevel, DateTime? referenceUtc = null)
            => DateTime.UtcNow.AddYears(1);

        public Task<bool> DeleteIfExpiredAsync(int userId, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<int> DeleteExpiredUsersAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<UserDeletionSummary?> DeleteUserWithContentAsync(
            int userId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<UserDeletionSummary?>(null);
    }
}
