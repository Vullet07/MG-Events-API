using Data;
using Data.Models;
using Microsoft.EntityFrameworkCore;
using WebAPI.Services.Quotas;

namespace MGEvents.Tests.Services;

public class UserActionQuotaServiceTests
{
    [Theory]
    [InlineData(UserActionQuotaType.EventPinCreate, 5)]
    [InlineData(UserActionQuotaType.ForumPostCreate, 20)]
    [InlineData(UserActionQuotaType.ForumThreadCreate, 1)]
    [InlineData(UserActionQuotaType.ReportCreate, 3)]
    [InlineData(UserActionQuotaType.ProfileUpdate, 3)]
    public async Task CheckAsync_WhenLimitReached_ReturnsBlocked(UserActionQuotaType actionType, int limit)
    {
        await using var db = CreateDbContext();
        var user = CreateUser();
        db.Users.Add(user);
        await db.SaveChangesAsync();

        AddQuotaEvents(db, user, actionType, limit, DateTime.UtcNow.AddMinutes(-5));
        await db.SaveChangesAsync();

        var service = new UserActionQuotaService(db);

        var result = await service.CheckAsync(user.Id, actionType);

        Assert.False(result.Allowed);
        Assert.Equal(string.Empty, ActionQuotaCheckResult.AllowedResult.Message);
        Assert.Contains("Достигна лимита", result.Message);
        Assert.True(result.RetryAfter > TimeSpan.Zero);
    }

    [Theory]
    [InlineData(UserActionQuotaType.EventPinCreate, 5)]
    [InlineData(UserActionQuotaType.ForumPostCreate, 20)]
    public async Task CheckAsync_AfterHourlyWindow_AllowsAgain(UserActionQuotaType actionType, int limit)
    {
        await using var db = CreateDbContext();
        var user = CreateUser();
        db.Users.Add(user);
        await db.SaveChangesAsync();

        AddQuotaEvents(db, user, actionType, limit, DateTime.UtcNow.AddHours(-2));
        await db.SaveChangesAsync();

        var service = new UserActionQuotaService(db);

        var result = await service.CheckAsync(user.Id, actionType);

        Assert.True(result.Allowed);
    }

    [Fact]
    public async Task CheckAsync_AfterReportDailyWindow_AllowsAgain()
    {
        await using var db = CreateDbContext();
        var user = CreateUser();
        db.Users.Add(user);
        await db.SaveChangesAsync();

        AddQuotaEvents(db, user, UserActionQuotaType.ReportCreate, 3, DateTime.UtcNow.AddHours(-25));
        await db.SaveChangesAsync();

        var service = new UserActionQuotaService(db);

        var result = await service.CheckAsync(user.Id, UserActionQuotaType.ReportCreate);

        Assert.True(result.Allowed);
    }

    [Fact]
    public async Task CheckAsync_AfterProfileDailyWindow_AllowsAgain()
    {
        await using var db = CreateDbContext();
        var user = CreateUser();
        db.Users.Add(user);
        await db.SaveChangesAsync();

        AddQuotaEvents(db, user, UserActionQuotaType.ProfileUpdate, 3, DateTime.UtcNow.AddHours(-25));
        await db.SaveChangesAsync();

        var service = new UserActionQuotaService(db);

        var result = await service.CheckAsync(user.Id, UserActionQuotaType.ProfileUpdate);

        Assert.True(result.Allowed);
    }

    [Fact]
    public async Task CheckAsync_AfterThreadDailyWindow_AllowsAgain()
    {
        await using var db = CreateDbContext();
        var user = CreateUser();
        db.Users.Add(user);
        await db.SaveChangesAsync();

        AddQuotaEvents(db, user, UserActionQuotaType.ForumThreadCreate, 1, DateTime.UtcNow.AddHours(-25));
        await db.SaveChangesAsync();

        var service = new UserActionQuotaService(db);

        var result = await service.CheckAsync(user.Id, UserActionQuotaType.ForumThreadCreate);

        Assert.True(result.Allowed);
    }

    [Fact]
    public async Task RecordAsync_WritesQuotaEvent()
    {
        await using var db = CreateDbContext();
        var user = CreateUser();
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var service = new UserActionQuotaService(db);

        await service.RecordAsync(user.Id, UserActionQuotaType.EventPinCreate);

        var stored = await db.UserActionQuotaEvents.SingleAsync();
        Assert.Equal(user.Id, stored.UserId);
        Assert.Equal(UserActionQuotaType.EventPinCreate, stored.ActionType);
    }

    [Fact]
    public async Task CleanupOldEventsAsync_RemovesEventsOlderThanFortyEightHours()
    {
        await using var db = CreateDbContext();
        var user = CreateUser();
        db.Users.Add(user);
        await db.SaveChangesAsync();

        db.UserActionQuotaEvents.AddRange(
            CreateQuotaEvent(user, UserActionQuotaType.EventPinCreate, DateTime.UtcNow.AddHours(-49)),
            CreateQuotaEvent(user, UserActionQuotaType.EventPinCreate, DateTime.UtcNow.AddHours(-1)));
        await db.SaveChangesAsync();

        var service = new UserActionQuotaService(db);

        var removed = await service.CleanupOldEventsAsync();

        Assert.Equal(1, removed);
        var remaining = await db.UserActionQuotaEvents.SingleAsync();
        Assert.True(remaining.CreatedAt > DateTime.UtcNow.AddHours(-48));
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new AppDbContext(options);
    }

    private static User CreateUser()
    {
        return new User
        {
            Username = $"quota-user-{Guid.NewGuid():N}",
            Email = $"quota-{Guid.NewGuid():N}@schoolmath.eu",
            PasswordHash = "hash",
            Role = Role.Student,
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
            db.UserActionQuotaEvents.Add(CreateQuotaEvent(user, actionType, startAt.AddMinutes(i)));
        }
    }

    private static UserActionQuotaEvent CreateQuotaEvent(
        User user,
        UserActionQuotaType actionType,
        DateTime createdAt)
    {
        return new UserActionQuotaEvent
        {
            UserId = user.Id,
            User = user,
            ActionType = actionType,
            CreatedAt = createdAt
        };
    }
}
