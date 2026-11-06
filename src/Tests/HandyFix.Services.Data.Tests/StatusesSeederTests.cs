namespace HandyFix.Services.Data.Tests
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Data;
    using HandyFix.Data.Seeding;

    using Microsoft.EntityFrameworkCore;

    using Xunit;

    public class StatusesSeederTests
    {
        // The code names every status through BookingStatusNames and PaymentStatusNames. These
        // two tests spell the names out on purpose: they are what the live database holds, so a
        // constant changed by mistake fails here instead of finding no status at run time.
        [Fact]
        public async Task SeedAsyncShouldWriteTheBookingStatusNamesTheDatabaseHolds()
        {
            using ApplicationDbContext dbContext = NewContext();

            await new BookingStatusesSeeder().SeedAsync(dbContext, null);
            await dbContext.SaveChangesAsync();

            Assert.Equal(
                new[] { "Abandoned", "Approved", "Cancelled", "Completed", "InProgress", "Pending" },
                dbContext.BookingStatuses.Select(x => x.Name).OrderBy(x => x).ToArray());
        }

        [Fact]
        public async Task SeedAsyncShouldWriteThePaymentStatusNamesTheDatabaseHolds()
        {
            using ApplicationDbContext dbContext = NewContext();

            await new PaymentStatusesSeeder().SeedAsync(dbContext, null);
            await dbContext.SaveChangesAsync();

            Assert.Equal(
                new[] { "Cancelled", "Completed", "Confirmed", "DepositPaid", "Pending", "Refunded" },
                dbContext.PaymentStatuses.Select(x => x.Name).OrderBy(x => x).ToArray());
        }

        private static ApplicationDbContext NewContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;

            return new ApplicationDbContext(options);
        }
    }
}
