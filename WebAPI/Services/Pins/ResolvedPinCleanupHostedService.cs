using Data;
using Data.Models;
using Microsoft.EntityFrameworkCore;

namespace WebAPI.Services.Pins
{
    public class ResolvedPinCleanupHostedService : BackgroundService
    {
        private static readonly TimeSpan PollInterval = TimeSpan.FromHours(12);
        private static readonly TimeSpan RetentionWindow = TimeSpan.FromDays(90);

        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ResolvedPinCleanupHostedService> _logger;

        public ResolvedPinCleanupHostedService(
            IServiceProvider serviceProvider,
            ILogger<ResolvedPinCleanupHostedService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var env = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();
                    var deletedCount = await CleanupResolvedPinsAsync(db, env, stoppingToken);
                    if (deletedCount > 0)
                    {
                        _logger.LogInformation("Resolved pin cleanup removed {DeletedCount} archived pins.", deletedCount);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Resolved pin cleanup failed.");
                }

                try
                {
                    await Task.Delay(PollInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }

        public static async Task<int> CleanupResolvedPinsAsync(AppDbContext db, IWebHostEnvironment env, CancellationToken cancellationToken)
        {
            var threshold = DateTime.UtcNow.Subtract(RetentionWindow);
            var stalePins = await db.EventPins
                .Include(pin => pin.CreatedByUser)
                .Where(pin => pin.IsResolved && pin.ArchivedAt != null && pin.ArchivedAt <= threshold)
                .ToListAsync(cancellationToken);

            if (stalePins.Count == 0)
            {
                return 0;
            }

            var stalePinIds = stalePins.Select(pin => pin.Id).ToList();
            var reports = await db.Reports
                .Where(report => report.TargetType == ReportTargetType.Pin && stalePinIds.Contains(report.TargetId))
                .ToListAsync(cancellationToken);

            if (reports.Count > 0)
            {
                db.Reports.RemoveRange(reports);
            }

            foreach (var pin in stalePins)
            {
                DeleteLocalMedia(env, pin.PhotoUrl);
            }

            db.EventPins.RemoveRange(stalePins);
            await db.SaveChangesAsync(cancellationToken);
            return stalePins.Count;
        }

        private static void DeleteLocalMedia(IWebHostEnvironment env, string? mediaUrl)
        {
            if (string.IsNullOrWhiteSpace(mediaUrl))
                return;

            var webRoot = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
            string relativePath;

            if (Uri.TryCreate(mediaUrl, UriKind.Absolute, out var absoluteUri))
            {
                relativePath = absoluteUri.AbsolutePath.TrimStart('/');
            }
            else
            {
                relativePath = mediaUrl.TrimStart('/', '\\');
            }

            var fullPath = Path.GetFullPath(Path.Combine(webRoot, relativePath.Replace("/", Path.DirectorySeparatorChar.ToString())));
            var rootPath = Path.GetFullPath(webRoot);

            if (!fullPath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase))
                return;

            if (System.IO.File.Exists(fullPath))
            {
                System.IO.File.Delete(fullPath);
            }
        }
    }
}
