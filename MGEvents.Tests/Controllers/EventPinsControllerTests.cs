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
using WebAPI.Services.Quotas;

namespace MGEvents.Tests.Controllers;

public class EventPinsControllerTests
{
    [Fact]
    public async Task GetAll_DefaultStatus_ExcludesResolvedPins()
    {
        await using var db = CreateDbContext();
        var user = CreateUser("pin-owner");
        var activePin = CreatePin(user, "ÐÐºÑ‚Ð¸Ð²ÐµÐ½ Ð¿Ð¸Ð½", false);
        var resolvedPin = CreatePin(user, "Ð Ð°Ð·Ñ€ÐµÑˆÐµÐ½ Ð¿Ð¸Ð½", true);

        db.Users.Add(user);
        db.EventPins.AddRange(activePin, resolvedPin);
        await db.SaveChangesAsync();

        var controller = CreateController(db);

        var result = await controller.GetAll();

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<ApiResponse<List<EventPinDto>>>(ok.Value);
        Assert.True(payload.Success);
        Assert.Single(payload.Data!);
        Assert.Equal(activePin.Title, payload.Data![0].Title);
    }

    [Fact]
    public async Task GetMonthlyReport_IncludesResolvedPinsInStatistics()
    {
        await using var db = CreateDbContext();
        var user = CreateUser("stats-owner");
        var activePin = CreatePin(user, "ÐÐºÑ‚Ð¸Ð²ÐµÐ½ Ð¿Ñ€Ð¾Ð±Ð»ÐµÐ¼", false);
        var resolvedPin = CreatePin(user, "Ð Ð°Ð·Ñ€ÐµÑˆÐµÐ½ Ð¿Ñ€Ð¾Ð±Ð»ÐµÐ¼", true);

        activePin.CreatedAt = DateTime.UtcNow.AddDays(-1);
        resolvedPin.CreatedAt = DateTime.UtcNow.AddDays(-1);

        db.Users.Add(user);
        db.EventPins.AddRange(activePin, resolvedPin);
        await db.SaveChangesAsync();

        var controller = CreateController(db, new TestAuthUserService { IdValue = 999, RoleValue = Role.Admin });

        var result = await controller.GetMonthlyReport(DateTime.UtcNow.ToString("yyyy-MM"));

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<ApiResponse<PinMonthlyReportDto>>(ok.Value);
        Assert.True(payload.Success);
        Assert.NotNull(payload.Data);
        Assert.Equal(2, payload.Data!.TotalPins);
        Assert.Equal(2, payload.Data.TopPins.Count);
    }

    [Fact]
    public async Task Resolve_StudentFirstConfirmation_DoesNotResolvePinYet()
    {
        await using var db = CreateDbContext();
        var owner = CreateUser("owner");
        var confirmer = CreateUser("student-confirm");
        var pin = CreatePin(owner, "Ð¡Ð¸Ð³Ð½Ð°Ð» Ð·Ð° Ð¿Ð¾Ð´Ð´Ñ€ÑŠÐ¶ÐºÐ°", false);

        db.Users.AddRange(owner, confirmer);
        db.EventPins.Add(pin);
        await db.SaveChangesAsync();

        var controller = CreateController(db, new TestAuthUserService { IdValue = confirmer.Id, RoleValue = Role.Student });

        var result = await controller.Resolve(pin.Id);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<ApiResponse<EventPinDto>>(ok.Value);
        Assert.True(payload.Success);
        Assert.NotNull(payload.Data);
        Assert.False(payload.Data!.IsResolved);
        Assert.Equal(1, payload.Data.ResolveConfirmationCount);
        Assert.True(payload.Data.HasCurrentUserResolveConfirmation);
        Assert.Contains("1/3", payload.Message);
    }

    [Fact]
    public async Task Resolve_ThirdStudentConfirmation_ResolvesPin()
    {
        await using var db = CreateDbContext();
        var owner = CreateUser("owner");
        var first = CreateUser("student-one");
        var second = CreateUser("student-two");
        var third = CreateUser("student-three");
        var pin = CreatePin(owner, "ÐŸÑ€Ð¾Ð±Ð»ÐµÐ¼ Ð² ÐºÐ¾Ñ€Ð¸Ð´Ð¾Ñ€Ð°", false);

        db.Users.AddRange(owner, first, second, third);
        db.EventPins.Add(pin);
        await db.SaveChangesAsync();

        db.EventPinResolveConfirmations.AddRange(
            new EventPinResolveConfirmation { PinId = pin.Id, Pin = pin, UserId = first.Id, User = first },
            new EventPinResolveConfirmation { PinId = pin.Id, Pin = pin, UserId = second.Id, User = second });
        await db.SaveChangesAsync();

        var controller = CreateController(db, new TestAuthUserService { IdValue = third.Id, RoleValue = Role.Student });

        var result = await controller.Resolve(pin.Id);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<ApiResponse<EventPinDto>>(ok.Value);
        Assert.True(payload.Success);
        Assert.NotNull(payload.Data);
        Assert.True(payload.Data!.IsResolved);
        Assert.Equal(3, payload.Data.ResolveConfirmationCount);
        Assert.Equal(third.Id, payload.Data.ResolvedByUserId);
        Assert.Contains("прагът от 3", payload.Message);
    }

    [Fact]
    public async Task Resolve_AdminFirstConfirmation_DoesNotResolvePinImmediately()
    {
        await using var db = CreateDbContext();
        var owner = CreateUser("owner");
        var admin = CreateUser("admin-user", Role.Admin);
        var pin = CreatePin(owner, "ÐŸÑ€Ð¾Ð±Ð»ÐµÐ¼ Ð² Ð´Ð²Ð¾Ñ€Ð°", false);

        db.Users.AddRange(owner, admin);
        db.EventPins.Add(pin);
        await db.SaveChangesAsync();

        var controller = CreateController(db, new TestAuthUserService { IdValue = admin.Id, RoleValue = Role.Admin });

        var result = await controller.Resolve(pin.Id);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<ApiResponse<EventPinDto>>(ok.Value);
        Assert.True(payload.Success);
        Assert.NotNull(payload.Data);
        Assert.False(payload.Data!.IsResolved);
        Assert.Equal(1, payload.Data.ResolveConfirmationCount);
        Assert.True(payload.Data.HasCurrentUserResolveConfirmation);
        Assert.Contains("1/3", payload.Message);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new AppDbContext(options);
    }

    private static EventPinsController CreateController(AppDbContext db, TestAuthUserService? authUser = null)
    {
        var controller = new EventPinsController(
            db,
            authUser ?? new TestAuthUserService(),
            new TestWebHostEnvironment(),
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

    private static EventPin CreatePin(User user, string title, bool isResolved)
    {
        var (latitude, longitude) = IndoorMapGeometry.EncodeLayerPoint("campus", 600d, isResolved ? 150d : 250d);
        return new EventPin
        {
            Title = title,
            Description = "ÐžÐ¿Ð¸ÑÐ°Ð½Ð¸Ðµ",
            Category = "ÐŸÐ¾Ð´Ð´Ñ€ÑŠÐ¶ÐºÐ°",
            Latitude = latitude,
            Longitude = longitude,
            CreatedByUser = user,
            CreatedAt = DateTime.UtcNow,
            IsResolved = isResolved,
            ResolvedAt = isResolved ? DateTime.UtcNow.AddDays(-2) : null,
            ArchivedAt = isResolved ? DateTime.UtcNow.AddDays(-2) : null,
            ResolvedByUser = isResolved ? user : null,
            ResolvedByUserId = isResolved ? user.Id : null
        };
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
}

