using Microsoft.Extensions.Caching.Memory;

namespace WebAPI.Services.Security
{
    public class LoginAttemptQuarantineService : ILoginAttemptQuarantineService
    {
        private const int MaxFailedAttemptsBeforeQuarantine = 5;
        private static readonly TimeSpan FailureWindow = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan QuarantineWindow = TimeSpan.FromMinutes(5);

        private readonly IMemoryCache _cache;

        public LoginAttemptQuarantineService(IMemoryCache cache)
        {
            _cache = cache;
        }

        public LoginQuarantineState GetState(string? remoteAddress)
        {
            var normalizedAddress = Normalize(remoteAddress);
            if (string.IsNullOrWhiteSpace(normalizedAddress))
                return new LoginQuarantineState(false, 0, null);

            if (_cache.TryGetValue<DateTimeOffset>(GetQuarantineKey(normalizedAddress), out var expiresAtUtc)
                && expiresAtUtc > DateTimeOffset.UtcNow)
            {
                return new LoginQuarantineState(true, MaxFailedAttemptsBeforeQuarantine + 1, expiresAtUtc);
            }

            var failedAttempts = _cache.Get<int?>(GetFailuresKey(normalizedAddress)) ?? 0;
            return new LoginQuarantineState(false, failedAttempts, null);
        }

        public LoginQuarantineState RegisterFailure(string? remoteAddress)
        {
            var normalizedAddress = Normalize(remoteAddress);
            if (string.IsNullOrWhiteSpace(normalizedAddress))
                return new LoginQuarantineState(false, 0, null);

            var currentState = GetState(normalizedAddress);
            if (currentState.IsQuarantined)
                return currentState;

            var failures = (_cache.Get<int?>(GetFailuresKey(normalizedAddress)) ?? 0) + 1;
            _cache.Set(GetFailuresKey(normalizedAddress), failures, DateTimeOffset.UtcNow.Add(FailureWindow));

            if (failures <= MaxFailedAttemptsBeforeQuarantine)
            {
                return new LoginQuarantineState(false, failures, null);
            }

            var expiresAtUtc = DateTimeOffset.UtcNow.Add(QuarantineWindow);
            _cache.Set(GetQuarantineKey(normalizedAddress), expiresAtUtc, expiresAtUtc);
            _cache.Remove(GetFailuresKey(normalizedAddress));

            return new LoginQuarantineState(true, failures, expiresAtUtc);
        }

        public void ClearFailures(string? remoteAddress)
        {
            var normalizedAddress = Normalize(remoteAddress);
            if (string.IsNullOrWhiteSpace(normalizedAddress))
                return;

            _cache.Remove(GetFailuresKey(normalizedAddress));
        }

        private static string? Normalize(string? remoteAddress) =>
            string.IsNullOrWhiteSpace(remoteAddress) ? null : remoteAddress.Trim().ToLowerInvariant();

        private static string GetFailuresKey(string remoteAddress) => $"auth:login:fail:{remoteAddress}";

        private static string GetQuarantineKey(string remoteAddress) => $"auth:login:quarantine:{remoteAddress}";
    }
}
