using Data;
using Microsoft.EntityFrameworkCore;
using Services.AuthUserService;
using WebAPI.Extensions;
using WebAPI.Services.Accounts;

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
            IAuthUserService authUser,
            AppDbContext db,
            IUserLifecycleService userLifecycleService)
        {
            if (authUser.IsAuthenticated && authUser.Id.HasValue)
            {
                var user = await db.Users
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(u => u.Id == authUser.Id.Value);

                if (user?.ScheduledDeletionAt != null && user.ScheduledDeletionAt <= DateTime.UtcNow)
                {
                    await userLifecycleService.DeleteUserWithContentAsync(user.Id);
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsJsonAsync(ApiResponse.Fail("Your student account expired and has been removed."));
                    return;
                }

                if (authUser.IsBanned)
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
            }

            await _next(context);
        }
    }
}
