using Data;
using Data.Models;
using Microsoft.EntityFrameworkCore;

namespace WebAPI.Services.Quotas
{
    public class UserActionQuotaService : IUserActionQuotaService
    {
        private static readonly TimeSpan CleanupCutoff = TimeSpan.FromHours(48);
        private readonly AppDbContext _db;

        public UserActionQuotaService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<ActionQuotaCheckResult> CheckAsync(
            int userId,
            UserActionQuotaType actionType,
            CancellationToken cancellationToken = default)
        {
            var rule = GetRule(actionType);
            var now = DateTime.UtcNow;
            var windowStart = now.Subtract(rule.Window);

            var eventsInWindow = await _db.UserActionQuotaEvents
                .Where(e => e.UserId == userId
                            && e.ActionType == actionType
                            && e.CreatedAt >= windowStart)
                .OrderBy(e => e.CreatedAt)
                .Select(e => e.CreatedAt)
                .Take(rule.Limit)
                .ToListAsync(cancellationToken);

            if (eventsInWindow.Count < rule.Limit)
            {
                return ActionQuotaCheckResult.AllowedResult;
            }

            var retryAfter = eventsInWindow[0].Add(rule.Window).Subtract(now);
            if (retryAfter < TimeSpan.Zero)
            {
                retryAfter = TimeSpan.Zero;
            }

            var message =
                $"Достигна лимита за {rule.Label}. Опитай отново след {FormatRetryAfter(retryAfter)}.";

            return new ActionQuotaCheckResult(false, message, retryAfter);
        }

        public async Task RecordAsync(
            int userId,
            UserActionQuotaType actionType,
            CancellationToken cancellationToken = default)
        {
            var user = await _db.Users.FindAsync(new object[] { userId }, cancellationToken);
            if (user == null)
            {
                throw new InvalidOperationException("Cannot record quota event for a missing user.");
            }

            _db.UserActionQuotaEvents.Add(new UserActionQuotaEvent
            {
                UserId = userId,
                User = user,
                ActionType = actionType,
                CreatedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task<int> CleanupOldEventsAsync(CancellationToken cancellationToken = default)
        {
            var cutoff = DateTime.UtcNow.Subtract(CleanupCutoff);
            var oldEvents = await _db.UserActionQuotaEvents
                .Where(e => e.CreatedAt < cutoff)
                .ToListAsync(cancellationToken);

            if (oldEvents.Count == 0)
            {
                return 0;
            }

            _db.UserActionQuotaEvents.RemoveRange(oldEvents);
            await _db.SaveChangesAsync(cancellationToken);
            return oldEvents.Count;
        }

        private static ActionQuotaRule GetRule(UserActionQuotaType actionType)
        {
            return actionType switch
            {
                UserActionQuotaType.EventPinCreate => new(5, TimeSpan.FromHours(1), "създаване на пинове"),
                UserActionQuotaType.ForumPostCreate => new(20, TimeSpan.FromHours(1), "публикуване на коментари"),
                UserActionQuotaType.ForumThreadCreate => new(1, TimeSpan.FromHours(24), "създаване на теми"),
                UserActionQuotaType.ReportCreate => new(5, TimeSpan.FromHours(1), "подаване на сигнали"),
                _ => throw new ArgumentOutOfRangeException(nameof(actionType), actionType, null)
            };
        }

        private static string FormatRetryAfter(TimeSpan retryAfter)
        {
            var totalMinutes = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalMinutes));
            if (totalMinutes < 60)
            {
                return $"{totalMinutes} мин.";
            }

            var hours = totalMinutes / 60;
            var minutes = totalMinutes % 60;
            return minutes == 0 ? $"{hours} ч." : $"{hours} ч. и {minutes} мин.";
        }

        private sealed record ActionQuotaRule(int Limit, TimeSpan Window, string Label);
    }
}
