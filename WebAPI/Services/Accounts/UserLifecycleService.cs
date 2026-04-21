using Data;
using Data.Models;
using Microsoft.EntityFrameworkCore;

namespace WebAPI.Services.Accounts
{
    public class UserLifecycleService : IUserLifecycleService
    {
        private readonly AppDbContext _db;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<UserLifecycleService> _logger;

        public UserLifecycleService(AppDbContext db, IWebHostEnvironment env, ILogger<UserLifecycleService> logger)
        {
            _db = db;
            _env = env;
            _logger = logger;
        }

        public int DetermineSchoolYearStart(DateTime? referenceUtc = null) =>
            StudentAccountPolicy.DetermineSchoolYearStart(referenceUtc ?? DateTime.UtcNow);

        public DateTime CalculateScheduledDeletionUtc(int gradeLevel, DateTime? referenceUtc = null) =>
            StudentAccountPolicy.CalculateScheduledDeletionUtc(gradeLevel, referenceUtc ?? DateTime.UtcNow);

        public async Task<bool> DeleteIfExpiredAsync(int userId, CancellationToken cancellationToken = default)
        {
            var user = await _db.Users
                .IgnoreQueryFilters()
                .Where(u => u.Id == userId)
                .Select(u => new { u.Id, u.ScheduledDeletionAt })
                .FirstOrDefaultAsync(cancellationToken);

            if (user?.ScheduledDeletionAt == null || user.ScheduledDeletionAt > DateTime.UtcNow)
                return false;

            await DeleteUserWithContentAsync(user.Id, cancellationToken);
            return true;
        }

        public async Task<int> DeleteExpiredUsersAsync(CancellationToken cancellationToken = default)
        {
            var expiredIds = await _db.Users
                .IgnoreQueryFilters()
                .Where(u =>
                    !u.IsDeleted &&
                    u.ScheduledDeletionAt != null &&
                    u.ScheduledDeletionAt <= DateTime.UtcNow &&
                    u.Role == Role.Student)
                .Select(u => u.Id)
                .ToListAsync(cancellationToken);

            var deletedCount = 0;
            foreach (var userId in expiredIds)
            {
                var result = await DeleteUserWithContentAsync(userId, cancellationToken);
                if (result != null)
                {
                    deletedCount++;
                }
            }

            return deletedCount;
        }

        public async Task<UserDeletionSummary?> DeleteUserWithContentAsync(int userId, CancellationToken cancellationToken = default)
        {
            var user = await _db.Users
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

            if (user == null)
                return null;

            await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

            var threadIds = await _db.ForumThreads
                .Where(t => EF.Property<int>(t, "CreatedByUserId") == userId)
                .Select(t => t.Id)
                .ToListAsync(cancellationToken);

            var threadPostIds = threadIds.Count == 0
                ? new List<int>()
                : await _db.ForumPosts
                    .Where(p => threadIds.Contains(EF.Property<int>(p, "ThreadId")))
                    .Select(p => p.Id)
                    .ToListAsync(cancellationToken);

            var directUserPostRefs = await _db.ForumPosts
                .Where(p =>
                    EF.Property<int>(p, "UserId") == userId &&
                    !threadIds.Contains(EF.Property<int>(p, "ThreadId")))
                .Select(p => new
                {
                    Post = p,
                    ThreadId = EF.Property<int>(p, "ThreadId")
                })
                .ToListAsync(cancellationToken);

            var directUserPosts = directUserPostRefs.Select(x => x.Post).ToList();
            var directPostIds = directUserPosts.Select(p => p.Id).ToHashSet();
            var hasRelationshipFixups = false;

            var pinIds = await _db.EventPins
                .Where(p => EF.Property<int>(p, "CreatedByUserId") == userId)
                .Select(p => p.Id)
                .ToListAsync(cancellationToken);

            var allRemovedPostIds = threadPostIds.Concat(directPostIds).Distinct().ToList();

            if (allRemovedPostIds.Count > 0)
            {
                // Rewire or detach every reply chain that points to a post we are about to delete.
                // This keeps SQL Server happy even when there are deep reply chains or mixed authors.
                var postsNeedingFixup = await _db.ForumPosts
                    .Where(p => p.ParentPostId != null)
                    .ToListAsync(cancellationToken);

                var postsById = postsNeedingFixup.ToDictionary(p => p.Id);

                foreach (var post in postsNeedingFixup)
                {
                    if (post.ParentPostId == null || !allRemovedPostIds.Contains(post.ParentPostId.Value))
                        continue;

                    int? nextParentId = post.ParentPostId;
                    var safety = 0;

                    while (nextParentId.HasValue && allRemovedPostIds.Contains(nextParentId.Value) && safety++ < 1024)
                    {
                        if (allRemovedPostIds.Contains(post.Id))
                        {
                            nextParentId = null;
                            break;
                        }

                        nextParentId = postsById.TryGetValue(nextParentId.Value, out var parentPost)
                            ? parentPost.ParentPostId
                            : null;
                    }

                    if (post.ParentPostId != nextParentId)
                    {
                        post.ParentPostId = nextParentId;
                        hasRelationshipFixups = true;
                    }
                }
            }

            var reportsResolvedByUser = await _db.Reports
                .Where(r => EF.Property<int?>(r, "ResolvedByUserId") == userId)
                .ToListAsync(cancellationToken);

            foreach (var report in reportsResolvedByUser)
            {
                report.ResolvedBy = null;
                report.ResolvedAt = null;
                hasRelationshipFixups = true;
            }

            var teacherRequestsReviewedByUser = await _db.TeacherRegistrationRequests
                .Where(r => EF.Property<int?>(r, "ReviewedByUserId") == userId)
                .ToListAsync(cancellationToken);

            foreach (var request in teacherRequestsReviewedByUser)
            {
                request.ReviewedBy = null;
                request.ReviewedAt = null;
                hasRelationshipFixups = true;
            }

            if (hasRelationshipFixups)
            {
                await _db.SaveChangesAsync(cancellationToken);
            }

            var reportsToRemove = await _db.Reports
                .Where(r =>
                    EF.Property<int>(r, "ReporterId") == userId ||
                    (r.TargetType == ReportTargetType.User && r.TargetId == userId) ||
                    (threadIds.Count > 0 && r.TargetType == ReportTargetType.Thread && threadIds.Contains(r.TargetId)) ||
                    (allRemovedPostIds.Count > 0 && r.TargetType == ReportTargetType.Post && allRemovedPostIds.Contains(r.TargetId)) ||
                    (pinIds.Count > 0 && r.TargetType == ReportTargetType.Pin && pinIds.Contains(r.TargetId)))
                .ToListAsync(cancellationToken);

            if (reportsToRemove.Count > 0)
            {
                _db.Reports.RemoveRange(reportsToRemove);
            }

            var postVotesByUser = await _db.PostVotes
                .Where(v => EF.Property<int>(v, "UserId") == userId)
                .ToListAsync(cancellationToken);

            if (postVotesByUser.Count > 0)
            {
                _db.PostVotes.RemoveRange(postVotesByUser);
            }

            var pinVotesByUser = await _db.PinVotes
                .Where(v => EF.Property<int>(v, "UserId") == userId)
                .ToListAsync(cancellationToken);

            if (pinVotesByUser.Count > 0)
            {
                _db.PinVotes.RemoveRange(pinVotesByUser);
            }

            var resolveConfirmationsByUser = await _db.EventPinResolveConfirmations
                .Where(c => c.UserId == userId)
                .ToListAsync(cancellationToken);

            if (resolveConfirmationsByUser.Count > 0)
            {
                _db.EventPinResolveConfirmations.RemoveRange(resolveConfirmationsByUser);
            }

            var userPasswordResetTokens = await _db.PasswordResetTokens
                .Where(t => EF.Property<int>(t, "UserId") == userId)
                .ToListAsync(cancellationToken);

            if (userPasswordResetTokens.Count > 0)
            {
                _db.PasswordResetTokens.RemoveRange(userPasswordResetTokens);
            }

            if (allRemovedPostIds.Count > 0)
            {
                var postsToRemove = await _db.ForumPosts
                    .Where(p => allRemovedPostIds.Contains(p.Id))
                    .ToListAsync(cancellationToken);

                if (postsToRemove.Count > 0)
                {
                    _db.ForumPosts.RemoveRange(postsToRemove);
                    await _db.SaveChangesAsync(cancellationToken);
                }
            }

            if (threadIds.Count > 0)
            {
                var threads = await _db.ForumThreads
                    .Where(t => threadIds.Contains(t.Id))
                    .ToListAsync(cancellationToken);

                if (threads.Count > 0)
                {
                    _db.ForumThreads.RemoveRange(threads);
                }
            }

            if (pinIds.Count > 0)
            {
                var pins = await _db.EventPins
                    .Where(p => pinIds.Contains(p.Id))
                    .ToListAsync(cancellationToken);

                if (pins.Count > 0)
                {
                    _db.EventPins.RemoveRange(pins);
                }
            }

            _db.Users.Remove(user);
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            DeleteUserMediaDirectories(userId);

            _logger.LogInformation(
                "Deleted user {UserId} ({Username}) with {RemovedThreads} threads, {RemovedPosts} standalone posts and {RemovedPins} pins.",
                user.Id,
                user.Username,
                threadIds.Count,
                directUserPosts.Count,
                pinIds.Count);

            return new UserDeletionSummary(
                user.Id,
                user.Username,
                user.Role,
                threadIds.Count,
                directUserPosts.Count,
                pinIds.Count);
        }

        private void DeleteUserMediaDirectories(int userId)
        {
            var webRoot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
            foreach (var mediaType in new[] { "users", "posts", "pins" })
            {
                var directory = Path.Combine(webRoot, "uploads", mediaType, userId.ToString());
                try
                {
                    if (Directory.Exists(directory))
                    {
                        Directory.Delete(directory, true);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete media directory {Directory} for user {UserId}.", directory, userId);
                }
            }
        }
    }
}
