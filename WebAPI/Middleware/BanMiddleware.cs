using Services.AuthUserService;
using WebAPI.Extensions;

namespace WebAPI.Middleware
{
    public class BanMiddleware
    {
        private readonly RequestDelegate _next;

        public BanMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(
            HttpContext context,
            IAuthUserService authUser)
        {
            // Only care about authenticated users
            if (authUser.IsAuthenticated && authUser.IsBanned)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/json";

                var response = ApiResponse.Fail(
                    authUser.BannedUntil != null
                        ? $"You are banned until {authUser.BannedUntil:yyyy-MM-dd HH:mm}."
                        : "You are permanently banned.");

                await context.Response.WriteAsJsonAsync(response);
                return;
            }

            await _next(context);
        }
    }
}
