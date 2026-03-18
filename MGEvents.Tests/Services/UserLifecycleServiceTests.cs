using Data;
using Data.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using WebAPI.Services.Accounts;

namespace MGEvents.Tests.Services;

public class UserLifecycleServiceTests
{
    [Fact]
    public async Task DeleteUserWithContentAsync_RemovesOwnedThreadWithNestedPosts()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDbContext(connection);

        var targetUser = CreateUser("thread-owner");
        var otherUser = CreateUser("replying-user");
        var thread = new ForumThread
        {
            Title = "Owned thread",
            CreatedByUser = targetUser
        };
        var parentPost = new ForumPost
        {
            Content = "Parent post",
            User = targetUser,
            Thread = thread
        };
        var replyPost = new ForumPost
        {
            Content = "Reply post",
            User = otherUser,
            Thread = thread,
            ParentPost = parentPost
        };

        db.Users.AddRange(targetUser, otherUser);
        db.ForumThreads.Add(thread);
        db.ForumPosts.AddRange(parentPost, replyPost);
        await db.SaveChangesAsync();

        var service = CreateService(db);

        var result = await service.DeleteUserWithContentAsync(targetUser.Id);

        Assert.NotNull(result);
        Assert.False(await db.Users.IgnoreQueryFilters().AnyAsync(user => user.Id == targetUser.Id));
        Assert.False(await db.ForumThreads.AnyAsync(item => item.Id == thread.Id));
        Assert.False(await db.ForumPosts.AnyAsync(item => item.Id == parentPost.Id || item.Id == replyPost.Id));
    }

    [Fact]
    public async Task DeleteUserWithContentAsync_ReparentsRemainingRepliesForSharedThread()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDbContext(connection);

        var targetUser = CreateUser("target-student");
        var threadOwner = CreateUser("thread-owner");
        var survivingUser = CreateUser("surviving-user");
        var thread = new ForumThread
        {
            Title = "Shared thread",
            CreatedByUser = threadOwner
        };
        var firstDeletedPost = new ForumPost
        {
            Content = "Deleted root reply",
            User = targetUser,
            Thread = thread
        };
        var secondDeletedPost = new ForumPost
        {
            Content = "Deleted nested reply",
            User = targetUser,
            Thread = thread,
            ParentPost = firstDeletedPost
        };
        var survivingReply = new ForumPost
        {
            Content = "This one should remain",
            User = survivingUser,
            Thread = thread,
            ParentPost = secondDeletedPost
        };

        db.Users.AddRange(targetUser, threadOwner, survivingUser);
        db.ForumThreads.Add(thread);
        db.ForumPosts.AddRange(firstDeletedPost, secondDeletedPost, survivingReply);
        await db.SaveChangesAsync();

        var service = CreateService(db);

        var result = await service.DeleteUserWithContentAsync(targetUser.Id);
        var remainingPost = await db.ForumPosts.SingleAsync(item => item.Id == survivingReply.Id);

        Assert.NotNull(result);
        Assert.False(await db.ForumPosts.AnyAsync(item => item.Id == firstDeletedPost.Id || item.Id == secondDeletedPost.Id));
        Assert.Equal(thread.Id, db.Entry(remainingPost).Property<int>("ThreadId").CurrentValue);
        Assert.Null(remainingPost.ParentPostId);
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

    private static UserLifecycleService CreateService(AppDbContext db)
    {
        var rootPath = Path.Combine(Path.GetTempPath(), "mg-events-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(rootPath);

        return new UserLifecycleService(
            db,
            new TestWebHostEnvironment(rootPath),
            NullLogger<UserLifecycleService>.Instance);
    }

    private static User CreateUser(string username, Role role = Role.Student)
    {
        return new User
        {
            Username = username,
            Email = $"{username}@example.com",
            PasswordHash = "hash",
            Role = role
        };
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
