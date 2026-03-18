namespace WebAPI.Services.Accounts
{
    public class ExpiredStudentCleanupHostedService : BackgroundService
    {
        private static readonly TimeSpan PollInterval = TimeSpan.FromHours(6);

        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ExpiredStudentCleanupHostedService> _logger;

        public ExpiredStudentCleanupHostedService(
            IServiceProvider serviceProvider,
            ILogger<ExpiredStudentCleanupHostedService> logger)
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
                    var lifecycleService = scope.ServiceProvider.GetRequiredService<IUserLifecycleService>();
                    var deletedCount = await lifecycleService.DeleteExpiredUsersAsync(stoppingToken);
                    if (deletedCount > 0)
                    {
                        _logger.LogInformation("Expired student cleanup removed {DeletedCount} accounts.", deletedCount);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Expired student cleanup failed.");
                }

                await Task.Delay(PollInterval, stoppingToken);
            }
        }
    }
}
