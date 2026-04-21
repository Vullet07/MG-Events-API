using Microsoft.Extensions.Caching.Memory;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace WebAPI.Middleware
{
    public class RateLimitingMiddleware
    {
        private readonly RequestDelegate _next;
        private static readonly MemoryCache Cache = new(new MemoryCacheOptions());
        private static readonly object CounterLock = new();

        private static readonly TimeSpan GlobalWindow = TimeSpan.FromMinutes(1);
        private static readonly TimeSpan BurstWindow = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan SensitiveWindow = TimeSpan.FromMinutes(5);

        private const int AuthenticatedGlobalLimit = 180;
        private const int AnonymousGlobalLimit = 90;
        private const int AuthenticatedBurstLimit = 45;
        private const int AnonymousBurstLimit = 24;
        private const int SensitiveEndpointLimit = 12;

        public RateLimitingMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var identity = GetClientIdentity(context);
            var isAuthenticated = context.User.Identity?.IsAuthenticated == true;

            if (!TryRegisterHit(
                    $"global:{identity}",
                    isAuthenticated ? AuthenticatedGlobalLimit : AnonymousGlobalLimit,
                    GlobalWindow,
                    out var retryAfter))
            {
                await RejectAsync(context, retryAfter);
                return;
            }

            if (!TryRegisterHit(
                    $"burst:{identity}",
                    isAuthenticated ? AuthenticatedBurstLimit : AnonymousBurstLimit,
                    BurstWindow,
                    out retryAfter))
            {
                await RejectAsync(context, retryAfter);
                return;
            }

            if (IsSensitiveEndpoint(context.Request.Path)
                && !TryRegisterHit($"sensitive:{identity}", SensitiveEndpointLimit, SensitiveWindow, out retryAfter))
            {
                await RejectAsync(context, retryAfter);
                return;
            }

            await _next(context);
        }

        private static bool TryRegisterHit(string key, int limit, TimeSpan window, out TimeSpan retryAfter)
        {
            var now = DateTimeOffset.UtcNow;

            lock (CounterLock)
            {
                if (!Cache.TryGetValue<RequestCounter>(key, out var counter) || counter == null || counter.ExpiresAt <= now)
                {
                    Cache.Set(key, new RequestCounter(1, now.Add(window)), now.Add(window));
                    retryAfter = TimeSpan.Zero;
                    return true;
                }

                if (counter.Count >= limit)
                {
                    retryAfter = counter.ExpiresAt - now;
                    return false;
                }

                counter.Count++;
                Cache.Set(key, counter, counter.ExpiresAt);
                retryAfter = TimeSpan.Zero;
                return true;
            }
        }

        private static string GetClientIdentity(HttpContext context)
        {
            if (context.User.Identity?.IsAuthenticated == true)
            {
                var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? context.User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                    ?? context.User.FindFirstValue("sub");

                if (!string.IsNullOrWhiteSpace(userId))
                {
                    return $"user:{userId}";
                }
            }

            var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            var ip = !string.IsNullOrWhiteSpace(forwardedFor)
                ? forwardedFor.Split(',').FirstOrDefault()?.Trim()
                : context.Connection.RemoteIpAddress?.ToString();

            return $"ip:{ip ?? "unknown"}";
        }

        private static bool IsSensitiveEndpoint(PathString path)
        {
            var value = path.Value?.ToLowerInvariant() ?? string.Empty;
            return value.Contains("/login")
                || value.Contains("/register")
                || value.Contains("/forgot-password")
                || value.Contains("/reset-password")
                || value.Contains("/confirm-email");
        }

        private static async Task RejectAsync(HttpContext context, TimeSpan retryAfter)
        {
            var retrySeconds = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.Headers.RetryAfter = retrySeconds.ToString();
            await context.Response.WriteAsJsonAsync(new
            {
                Success = false,
                Message = $"Твърде много заявки. Опитай отново след {retrySeconds} секунди."
            });
        }

        private sealed class RequestCounter
        {
            public RequestCounter(int count, DateTimeOffset expiresAt)
            {
                Count = count;
                ExpiresAt = expiresAt;
            }

            public int Count { get; set; }
            public DateTimeOffset ExpiresAt { get; }
        }
    }
}
