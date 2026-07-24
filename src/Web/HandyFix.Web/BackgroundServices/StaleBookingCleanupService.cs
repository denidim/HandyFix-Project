namespace HandyFix.Web.BackgroundServices
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using HandyFix.Services.Data.Bookings;

    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;

    public class StaleBookingCleanupService : BackgroundService
    {
        private static readonly TimeSpan RunInterval = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan AbandonAfter = TimeSpan.FromMinutes(15);

        private readonly IServiceScopeFactory scopeFactory;
        private readonly ILogger<StaleBookingCleanupService> logger;

        public StaleBookingCleanupService(IServiceScopeFactory scopeFactory, ILogger<StaleBookingCleanupService> logger)
        {
            this.scopeFactory = scopeFactory;
            this.logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = this.scopeFactory.CreateScope();
                    var bookingsService = scope.ServiceProvider.GetRequiredService<IBookingsService>();
                    var releasedCount = await bookingsService.ReleaseAbandonedBookingsAsync(AbandonAfter);

                    if (releasedCount > 0)
                    {
                        this.logger.LogInformation(
                            "Released {Count} abandoned booking(s) and freed their time slots.",
                            releasedCount);
                    }
                }
                catch (Exception ex)
                {
                    // Never let a single failed run kill the background loop.
                    this.logger.LogError(ex, "Stale booking cleanup run failed.");
                }

                try
                {
                    await Task.Delay(RunInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // Expected during application shutdown.
                }
            }
        }
    }
}
