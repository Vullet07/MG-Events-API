using Microsoft.Extensions.Caching.Memory;
using System.Security.Claims;

namespace WebAPI.Middleware
{
    public class RateLimitingMiddleware
    {
        private readonly RequestDelegate _next;
        private static readonly MemoryCache _cache = new MemoryCache(new MemoryCacheOptions());
        private const int DefaultLimit = 100; // default limit per minute

        public RateLimitingMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var path = context.Request.Path.ToString().ToLower();
            var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            // Set limit based on endpoint
            int limit = path.Contains("/forgot-password") ? 3 : DefaultLimit;
            var key = $"{ip}:{path}";

            if (!_cache.TryGetValue(key, out int count))
            {
                _cache.Set(key, 1, DateTimeOffset.UtcNow.AddMinutes(1));
            }
            else
            {
                if (count >= limit)
                {
                    context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                    await context.Response.WriteAsJsonAsync(new
                    {
                        Success = false,
                        Message = "Too many requests. Try again later."
                    });
                    return;
                }

                _cache.Set(key, count + 1, DateTimeOffset.UtcNow.AddMinutes(1));
            }

            await _next(context);
        }
    }
}
