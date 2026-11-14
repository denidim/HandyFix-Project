namespace HandyFix.Services.Data.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Common;
    using HandyFix.Data;
    using HandyFix.Data.Models;
    using HandyFix.Data.Repositories;
    using HandyFix.Services.Data.SiteCounts;
    using HandyFix.Web.ViewModels.Booking;

    using Microsoft.Data.Sqlite;
    using Microsoft.EntityFrameworkCore;

    using Xunit;

    // The website's count of what its visitors do, a sum for each day, and the bookings and
    // deposits the Dashboard shows beside it (PROJECT_STATE.md Section 3fa).
    //
    // The sums are tested on Sqlite, not on the in-memory provider: one is added by a statement
    // the database itself runs ("Count = Count + 1"), which the in-memory provider cannot run,
    // and "one row for a day and a kind" is a rule only a real database keeps.
    public sealed class SiteCountsTests : IDisposable
    {
        private static readonly DateOnly Monday = new DateOnly(2026, 11, 16);

        private readonly SqliteConnection connection;
        private readonly ApplicationDbContext dbContext;
        private readonly SiteCountsService service;

        public SiteCountsTests()
        {
            this.connection = new SqliteConnection("DataSource=:memory:");
            this.connection.Open();
            this.dbContext = new ApplicationDbContext(
                new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(this.connection).Options);
            this.dbContext.Database.EnsureCreated();
            this.service = new SiteCountsService(new EfRepository<SiteCount>(this.dbContext));
        }

        public void Dispose()
        {
            this.dbContext.Dispose();
            this.connection.Dispose();
        }

        [Fact]
        public async Task TheFirstOfADayMakesItsRowAndTheNextAreAddedToIt()
        {
            await this.service.AddAsync(SiteCountKind.CallTapped, Monday);
            await this.service.AddAsync(SiteCountKind.CallTapped, Monday);
            await this.service.AddAsync(SiteCountKind.CallTapped, Monday);

            SiteCount row = this.dbContext.SiteCounts.AsNoTracking().Single();
            Assert.Equal(Monday, row.Day);
            Assert.Equal(SiteCountKind.CallTapped, row.Kind);
            Assert.Equal(3, row.Count);
        }

        [Fact]
        public async Task EachKindAndEachDayHasItsOwnSumAndTheTotalsAddTheDaysUp()
        {
            DateOnly tuesday = Monday.AddDays(1);
            await this.service.AddAsync(SiteCountKind.VisitOnPhone, Monday);
            await this.service.AddAsync(SiteCountKind.VisitOnPhone, Monday);
            await this.service.AddAsync(SiteCountKind.VisitOnComputer, Monday);
            await this.service.AddAsync(SiteCountKind.VisitOnPhone, tuesday);

            Assert.Equal(3, this.dbContext.SiteCounts.AsNoTracking().Count());

            IReadOnlyDictionary<SiteCountKind, int> everyDay = await this.service.GetTotalsAsync(null);
            Assert.Equal(3, everyDay[SiteCountKind.VisitOnPhone]);
            Assert.Equal(1, everyDay[SiteCountKind.VisitOnComputer]);

            // A kind that never happened is not there, and the Dashboard shows it as nought.
            Assert.False(everyDay.ContainsKey(SiteCountKind.TimePicked));

            IReadOnlyDictionary<SiteCountKind, int> fromTuesday = await this.service.GetTotalsAsync(tuesday);
            Assert.Equal(1, fromTuesday[SiteCountKind.VisitOnPhone]);
            Assert.False(fromTuesday.ContainsKey(SiteCountKind.VisitOnComputer));
        }

        [Fact]
        public async Task TheFirstDayIsTheEarliestOneCountedAndThereIsNoneBeforeAnyCount()
        {
            Assert.Null(await this.service.GetFirstDayAsync());

            await this.service.AddAsync(SiteCountKind.BookingOpened, Monday.AddDays(2));
            await this.service.AddAsync(SiteCountKind.WhatsAppTapped, Monday);

            Assert.Equal(Monday, await this.service.GetFirstDayAsync());
        }

        // "Sent the booking" and "Paid the deposit" are the website's own bookings only. One
        // given up before paying was still sent, and a deposit refunded later was still paid.
        [Fact]
        public async Task TheWebsiteBookingsAreCountedWithHowManyPaidTheDeposit()
        {
            using var world = new BookingWorld();
            Booking paid = await world.SeedPaidBookingAsync();
            await world.SeedPendingPaymentAsync("7 Hook Rd, Chessington");
            await world.SeedWrittenInJobAsync();

            WebsiteBookingCounts counts = await world.Bookings.GetWebsiteBookingCountsAsync(null);
            Assert.Equal(2, counts.Sent);
            Assert.Equal(1, counts.DepositPaid);

            Payment deposit = world.DbContext.Payments.Single(x => x.BookingId == paid.Id);
            deposit.StatusId = world.DbContext.PaymentStatuses.Single(x => x.Name == PaymentStatusNames.Refunded).Id;
            await world.DbContext.SaveChangesAsync();
            Assert.Equal(1, (await world.Bookings.GetWebsiteBookingCountsAsync(null)).DepositPaid);

            // Only those made since the day that is given.
            paid.CreatedOn = DateTime.UtcNow.AddDays(-40);
            await world.DbContext.SaveChangesAsync();
            WebsiteBookingCounts lastMonth = await world.Bookings.GetWebsiteBookingCountsAsync(DateTime.UtcNow.AddDays(-30));
            Assert.Equal(1, lastMonth.Sent);
            Assert.Equal(0, lastMonth.DepositPaid);
        }
    }
}
