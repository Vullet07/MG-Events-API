using Data;
using Data.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Services.AuthUserService;
using Services.Dtos;
using WebAPI.Controllers;
using WebAPI.Extensions;
using WebAPI.Models;
using WebAPI.Services.Accounts;
using WebAPI.Services.Quotas;

namespace MGEvents.Tests.Controllers;

public class UserControllerTests
{
    [Fact]
    public async Task Update_AllowsCurrentUserToChangeUsernameWithoutEmail()
    {
        await using var db = CreateDbContext();
        var user = CreateUser("old-name");
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var controller = CreateController(db, new TestAuthUserService
        {
            IdValue = user.Id,
            RoleValue = Role.Student
        });

        var result = await controller.Update(user.Id, new UpdateUserDto
        {
            Username = "new-name"
        });

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<ApiResponse<UserDto>>(ok.Value);
        Assert.True(payload.Success);
        Assert.NotNull(payload.Data);
        Assert.Equal("new-name", payload.Data!.Username);

        var updated = await db.Users.SingleAsync(item => item.Id == user.Id);
        Assert.Equal("new-name", updated.Username);
        Assert.Equal("old-name@schoolmath.eu", updated.Email);
    }

    [Fact]
    public async Task UploadProfilePhoto_BindsMultipartFileAndUpdatesCurrentUser()
    {
        await using var db = CreateDbContext();
        var user = CreateUser("photo-user");
        db.Users.Add(user);
        await db.SaveChangesAsync();

        using var environment = new TestWebHostEnvironment();
        var controller = CreateController(
            db,
            new TestAuthUserService
            {
                IdValue = user.Id,
                RoleValue = Role.Student
            },
            environment);

        await using var imageStream = new MemoryStream([0xFF, 0xD8, 0xFF, 0xD9]);
        var formFile = new FormFile(imageStream, 0, imageStream.Length, "file", "avatar.jpg")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/jpeg"
        };

        var result = await controller.UploadProfilePhoto(formFile);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<ApiResponse<UserDto>>(ok.Value);
        Assert.True(payload.Success);
        Assert.NotNull(payload.Data);
        Assert.Contains($"/uploads/users/{user.Id}/", payload.Data!.PhotoUrl);

        var updated = await db.Users.SingleAsync(item => item.Id == user.Id);
        Assert.Equal(payload.Data.PhotoUrl, updated.PhotoUrl);
        Assert.True(Directory.EnumerateFiles(environment.WebRootPath, "*.jpg", SearchOption.AllDirectories).Any());
    }

    [Fact]
    public async Task UpdateMyProfile_UpdatesUsernameAndPhotoAsOneProfileQuotaEvent()
    {
        await using var db = CreateDbContext();
        var user = CreateUser("combined-user");
        db.Users.Add(user);
        await db.SaveChangesAsync();

        using var environment = new TestWebHostEnvironment();
        var controller = CreateController(
            db,
            new TestAuthUserService
            {
                IdValue = user.Id,
                RoleValue = Role.Student
            },
            environment);

        await using var imageStream = new MemoryStream([0xFF, 0xD8, 0xFF, 0xD9]);
        var form = new UpdateProfileForm
        {
            Username = "combined-new",
            File = new FormFile(imageStream, 0, imageStream.Length, "file", "avatar.jpg")
            {
                Headers = new HeaderDictionary(),
                ContentType = "image/jpeg"
            }
        };

        var result = await controller.UpdateMyProfile(form);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<ApiResponse<UserDto>>(ok.Value);
        Assert.True(payload.Success);
        Assert.Equal("combined-new", payload.Data!.Username);
        Assert.Contains($"/uploads/users/{user.Id}/", payload.Data.PhotoUrl);

        var quotaEvent = await db.UserActionQuotaEvents.SingleAsync();
        Assert.Equal(UserActionQuotaType.ProfileUpdate, quotaEvent.ActionType);
    }

    [Fact]
    public async Task UpdateMyProfile_WhenDailyProfileQuotaReached_ReturnsTooManyRequests()
    {
        await using var db = CreateDbContext();
        var user = CreateUser("limited-user");
        db.Users.Add(user);
        await db.SaveChangesAsync();

        AddQuotaEvents(db, user, UserActionQuotaType.ProfileUpdate, 3, DateTime.UtcNow.AddMinutes(-10));
        await db.SaveChangesAsync();

        var controller = CreateController(db, new TestAuthUserService
        {
            IdValue = user.Id,
            RoleValue = Role.Student
        });

        var result = await controller.UpdateMyProfile(new UpdateProfileForm
        {
            Username = "limited-new"
        });

        var tooManyRequests = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status429TooManyRequests, tooManyRequests.StatusCode);

        var updated = await db.Users.SingleAsync(item => item.Id == user.Id);
        Assert.Equal("limited-user", updated.Username);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new AppDbContext(options);
    }

    private static UserController CreateController(
        AppDbContext db,
        TestAuthUserService authUser,
        IWebHostEnvironment? environment = null)
    {
        var controller = new UserController(
            db,
            new PasswordHasher<User>(),
            authUser,
            environment ?? new TestWebHostEnvironment(),
            new StubUserLifecycleService(),
            new UserActionQuotaService(db));

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
        int count,
        DateTime startAt)
    {
        for (var i = 0; i < count; i++)
        {
            db.UserActionQuotaEvents.Add(new UserActionQuotaEvent
            {
                UserId = user.Id,
                User = user,
                ActionType = actionType,
                CreatedAt = startAt.AddMinutes(i)
            });
        }
    }

    private sealed class TestAuthUserService : IAuthUserService
    {
        public int? IdValue { get; init; }
        public string? EmailValue { get; init; }
        public string? UsernameValue { get; init; }
        public Role? RoleValue { get; init; }
        public bool IsAuthenticatedValue { get; init; } = true;
        public bool IsBannedValue { get; init; }
        public DateTime? BannedUntilValue { get; init; }

        public int? Id => IdValue;
        public string? Email => EmailValue;
        public string? Username => UsernameValue;
        public Role? Role => RoleValue;
        public bool IsAuthenticated => IsAuthenticatedValue;
        public bool IsBanned => IsBannedValue;
        public DateTime? BannedUntil => BannedUntilValue;
    }

    private sealed class StubUserLifecycleService : IUserLifecycleService
    {
        public int DetermineSchoolYearStart(DateTime? referenceUtc = null) => 2026;

        public DateTime CalculateScheduledDeletionUtc(int gradeLevel, DateTime? referenceUtc = null)
            => new(2027, 7, 1, 0, 0, 0, DateTimeKind.Utc);

        public Task<bool> DeleteIfExpiredAsync(int userId, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<int> DeleteExpiredUsersAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<UserDeletionSummary?> DeleteUserWithContentAsync(int userId, CancellationToken cancellationToken = default)
            => Task.FromResult<UserDeletionSummary?>(null);
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment, IDisposable
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

        public void Dispose()
        {
            if (Directory.Exists(ContentRootPath))
                Directory.Delete(ContentRootPath, recursive: true);
        }
    }
}
