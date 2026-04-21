using Data;
using Data.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using WebAPI.Services.Pins;

namespace MGEvents.Tests.Services;

public class ResolvedPinCleanupHostedServiceTests
{
    [Fact]
    public async Task CleanupResolvedPinsAsync_RemovesExpiredResolvedPinsReportsAndMedia()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDbContext(connection);

        var rootPath = Path.Combine(Path.GetTempPath(), "mg-events-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(rootPath);
        var env = new TestWebHostEnvironment(rootPath);

        var user = new User
        {
            Username = "pin-owner",
            Email = "pin-owner@schoolmath.eu",
            PasswordHash = "hash",
            Role = Role.Student,
            IsEmailConfirmed = true
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        var uploadsDirectory = Path.Combine(rootPath, "uploads", "pins", user.Id.ToString());
        Directory.CreateDirectory(uploadsDirectory);
        var filePath = Path.Combine(uploadsDirectory, "resolved.jpg");
        await File.WriteAllTextAsync(filePath, "fake-image");

        var pin = new EventPin
        {
            Title = "Resolved issue",
            Description = "Old resolved pin",
            Category = "Поддръжка",
            CreatedByUser = user,
            Latitude = 42.0,
            Longitude = 24.0,
            PhotoUrl = "https://localhost:7277/uploads/pins/" + user.Id + "/resolved.jpg",
            IsResolved = true,
            ArchivedAt = DateTime.UtcNow.AddDays(-91),
            ResolvedAt = DateTime.UtcNow.AddDays(-91),
            ResolvedByUserId = user.Id
        };

        db.EventPins.Add(pin);
        await db.SaveChangesAsync();

        db.Reports.Add(new Report
        {
            Reporter = user,
            TargetType = ReportTargetType.Pin,
            TargetId = pin.Id,
            Reason = "Needs review"
        });
        await db.SaveChangesAsync();

        var removedCount = await ResolvedPinCleanupHostedService.CleanupResolvedPinsAsync(db, env, CancellationToken.None);

        Assert.Equal(1, removedCount);
        Assert.False(await db.EventPins.AnyAsync(item => item.Id == pin.Id));
        Assert.False(await db.Reports.AnyAsync(item => item.TargetType == ReportTargetType.Pin && item.TargetId == pin.Id));
        Assert.False(File.Exists(filePath));
    }

    private static AppDbContext CreateDbContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public TestWebHostEnvironment(string rootPath)
        {
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
