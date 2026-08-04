namespace HandyFix.Web.Services
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Data;
    using HandyFix.Services.Data.Availability;

    using Microsoft.AspNetCore.Hosting;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Seeds a short window of booking capacity outside production, so a fresh clone or a newly
    /// stood-up staging box has a working booking flow without an admin having to open the
    /// Calendar first. In production capacity is strictly a business decision - nothing generates
    /// slots there except an admin.
    /// </summary>
    /// <remarks>
    /// This lives in the Web layer rather than alongside the ISeeder implementations in
    /// HandyFix.Data because it delegates to IAvailabilityService for the actual generation.
    /// HandyFix.Data has no reference to the services layer (by design), so a seeder there would
    /// have to restate the "9-17, no Sundays" rule as a second copy of the same business rule.
    /// </remarks>
    public static class DevelopmentCapacitySeeder
    {
        private const int DaysOfCapacity = 14;

        public static async Task SeedAsync(IServiceProvider serviceProvider, IWebHostEnvironment environment)
        {
            if (environment.IsProduction())
            {
                return;
            }

            ApplicationDbContext dbContext = serviceProvider.GetRequiredService<ApplicationDbContext>();
            DateTime today = DateTime.Today;

            // Only when there is no future capacity at all. This keeps the seeder quiet once a
            // real admin has generated (or deliberately blocked - blocked slots are still rows)
            // capacity of their own, while still topping up a long-lived staging database whose
            // originally seeded window has since fallen into the past.
            var hasFutureCapacity = await dbContext.AvailabilitySlots
                .AnyAsync(x => x.StartTime >= today);
            if (hasFutureCapacity)
            {
                return;
            }

            IAvailabilityService availabilityService = serviceProvider.GetRequiredService<IAvailabilityService>();
            await availabilityService.GenerateSlotsForRangeAsync(today, today.AddDays(DaysOfCapacity));

            serviceProvider.GetRequiredService<ILoggerFactory>()
                .CreateLogger(typeof(DevelopmentCapacitySeeder))
                .LogInformation(
                    "Seeded {Days} days of booking capacity for the {Environment} environment - no future slots existed.",
                    DaysOfCapacity,
                    environment.EnvironmentName);
        }
    }
}
