namespace WebAPI.Services.Quotas
{
    public class UserActionQuotaCleanupHostedService : BackgroundService
    {
        private static readonly TimeSpan InitialDelay = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan Interval = TimeSpan.FromHours(6);
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<UserActionQuotaCleanupHostedService> _logger;

        public UserActionQuotaCleanupHostedService(
            IServiceProvider serviceProvider,
            ILogger<UserActionQuotaCleanupHostedService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                await Task.Delay(InitialDelay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var quotaService = scope.ServiceProvider.GetRequiredService<IUserActionQuotaService>();
                    var removed = await quotaService.CleanupOldEventsAsync(stoppingToken);
                    if (removed > 0)
                    {
                        _logger.LogInformation("Removed {Count} old user action quota events.", removed);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to clean old user action quota events.");
                }

                try
                {
                    await Task.Delay(Interval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }
}
