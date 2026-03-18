using Data.Models;

namespace WebAPI.Services.Accounts
{
    public sealed record UserDeletionSummary(
        int UserId,
        string Username,
        Role Role,
        int RemovedThreads,
        int RemovedPosts,
        int RemovedPins);

    public interface IUserLifecycleService
    {
        int DetermineSchoolYearStart(DateTime? referenceUtc = null);
        DateTime CalculateScheduledDeletionUtc(int gradeLevel, DateTime? referenceUtc = null);
        Task<bool> DeleteIfExpiredAsync(int userId, CancellationToken cancellationToken = default);
        Task<int> DeleteExpiredUsersAsync(CancellationToken cancellationToken = default);
        Task<UserDeletionSummary?> DeleteUserWithContentAsync(int userId, CancellationToken cancellationToken = default);
    }
}
