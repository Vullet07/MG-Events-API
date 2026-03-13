using System.Security.Claims;
using Data;
using Data.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Services.AuthUserService;

namespace MGEvents.Tests.Services;

public class AuthUserServiceTests
{
    [Fact]
    public void ShouldReturnAuthenticatedUserData_WhenClaimContainsExistingUserId()
    {
        using var db = CreateDbContext();
        SeedUser(db, id: 5, username: "martin", role: Role.Student, email: "martin@test.local");

        var service = CreateService(db, userId: 5, isAuthenticated: true);

        Assert.True(service.IsAuthenticated);
        Assert.Equal(5, service.Id);
        Assert.Equal("martin", service.Username);
        Assert.Equal("martin@test.local", service.Email);
        Assert.Equal(Role.Student, service.Role);
    }

    [Fact]
    public void ShouldReturnNullUserData_WhenUserIsSoftDeleted()
    {
        using var db = CreateDbContext();
        SeedUser(db, id: 7, username: "deletedUser", role: Role.Student, email: "deleted@test.local", isDeleted: true);

        var service = CreateService(db, userId: 7, isAuthenticated: true);

        Assert.True(service.IsAuthenticated);
        Assert.Null(service.Id);
        Assert.Null(service.Username);
        Assert.Null(service.Email);
        Assert.Null(service.Role);
    }

    [Fact]
    public void IsAuthenticated_ShouldBeFalse_WhenIdentityIsNotAuthenticated()
    {
        using var db = CreateDbContext();

        var service = CreateService(db, userId: 1, isAuthenticated: false);

        Assert.False(service.IsAuthenticated);
        Assert.Null(service.Id);
    }

    [Fact]
    public void IsBanned_ShouldBeTrue_WhenUserIsBannedWithoutEndDate()
    {
        using var db = CreateDbContext();
        SeedUser(
            db,
            id: 10,
            username: "bannedUser",
            role: Role.Student,
            email: "banned@test.local",
            isBanned: true,
            bannedUntil: null);

        var service = CreateService(db, userId: 10, isAuthenticated: true);

        Assert.True(service.IsBanned);
        Assert.Null(service.BannedUntil);
    }

    [Fact]
    public void IsBanned_ShouldBeFalse_WhenBanExpirationIsInPast()
    {
        using var db = CreateDbContext();
        SeedUser(
            db,
            id: 11,
            username: "expiredBan",
            role: Role.Student,
            email: "expired@test.local",
            isBanned: true,
            bannedUntil: DateTime.UtcNow.AddMinutes(-5));

        var service = CreateService(db, userId: 11, isAuthenticated: true);

        Assert.False(service.IsBanned);
        Assert.NotNull(service.BannedUntil);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new AppDbContext(options);
    }

    private static AuthUserService CreateService(AppDbContext db, int userId, bool isAuthenticated)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId.ToString()) };
        var identity = isAuthenticated
            ? new ClaimsIdentity(claims, "TestAuth")
            : new ClaimsIdentity(claims);

        var contextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity)
            }
        };

        return new AuthUserService(contextAccessor, db);
    }

    private static void SeedUser(
        AppDbContext db,
        int id,
        string username,
        Role role,
        string email,
        bool isDeleted = false,
        bool isBanned = false,
        DateTime? bannedUntil = null)
    {
        db.Users.Add(new User
        {
            Id = id,
            Username = username,
            Email = email,
            PasswordHash = "hash",
            Role = role,
            IsDeleted = isDeleted,
            IsBanned = isBanned,
            BannedUntil = bannedUntil
        });

        db.SaveChanges();
    }
}
