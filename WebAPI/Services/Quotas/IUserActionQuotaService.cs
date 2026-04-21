using Data.Models;

namespace WebAPI.Services.Quotas
{
    public interface IUserActionQuotaService
    {
        Task<ActionQuotaCheckResult> CheckAsync(
            int userId,
            UserActionQuotaType actionType,
            CancellationToken cancellationToken = default);

        Task RecordAsync(
            int userId,
            UserActionQuotaType actionType,
            CancellationToken cancellationToken = default);

        Task<int> CleanupOldEventsAsync(CancellationToken cancellationToken = default);
    }
}
