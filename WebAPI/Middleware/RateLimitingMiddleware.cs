using Microsoft.Extensions.Caching.Memory;
using System.Security.Claims;

namespace WebAPI.Middleware
{
    public class RateLimitingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IMemoryCache _cache;

        private const int LIMIT = 100;
        private static readonly TimeSpan WINDOW = TimeSpan.FromMinutes(1);

        public RateLimitingMiddleware(RequestDelegate next, IMemoryCache cache)
        {
            _next = next;
            _cache = cache;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var key = GetClientKey(context);

            if (key == null)
            {
                await _next(context);
                return;
            }

            var counter = _cache.GetOrCreate(key, entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = WINDOW;
                return 0;
            });

            if (counter >= LIMIT)
            {
                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                await context.Response.WriteAsJsonAsync(new
                {
                    success = false,
                    message = "Too many requests. Please try again later."
                });
                return;
            }

            _cache.Set(key, counter + 1, WINDOW);

            await _next(context);
        }

        private static string? GetClientKey(HttpContext context)
        {
            if (context.User.Identity?.IsAuthenticated == true)
            {
                return context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            }

            return context.Connection.RemoteIpAddress?.ToString();
        }
    }
}
