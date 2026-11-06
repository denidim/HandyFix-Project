namespace HandyFix.Services.Data.Tests
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Data;
    using HandyFix.Data.Models;
    using HandyFix.Data.Repositories;
    using HandyFix.Services.Data.Availability;

    using Microsoft.EntityFrameworkCore;

    using Xunit;

    public class AvailabilityServiceTests
    {
        [Fact]
        public async Task BookSlotAsyncShouldMarkSlotAsBookedAndLinkBooking()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;

            using var dbContext = new ApplicationDbContext(options);
            using var slotRepository = new EfDeletableEntityRepository<AvailabilitySlot>(dbContext);

            var slot = new AvailabilitySlot { StartTime = DateTime.Today.AddHours(9), EndTime = DateTime.Today.AddHours(10), IsBooked = false };
            dbContext.AvailabilitySlots.Add(slot);
            await dbContext.SaveChangesAsync();

            var service = new AvailabilityService(slotRepository);
            var bookingId = Guid.NewGuid();
            var result = await service.BookSlotAsync(slot.Id, bookingId);

            Assert.True(result);
            var updatedSlot = dbContext.AvailabilitySlots.First(x => x.Id == slot.Id);
            Assert.True(updatedSlot.IsBooked);
            Assert.Equal(bookingId, updatedSlot.BookingId);
        }

        // The booking page opens again on the day of the slot a customer had chosen when their
        // form comes back to them (PROJECT_STATE.md Section 3cb).
        [Fact]
        public async Task GetSlotDateAsyncShouldGiveTheDayASlotIsOn()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;

            using var dbContext = new ApplicationDbContext(options);
            using var slotRepository = new EfDeletableEntityRepository<AvailabilitySlot>(dbContext);

            DateTime day = DateTime.Today.AddDays(5);
            var slot = new AvailabilitySlot { StartTime = day.AddHours(14), EndTime = day.AddHours(15) };
            dbContext.AvailabilitySlots.Add(slot);
            await dbContext.SaveChangesAsync();

            var service = new AvailabilityService(slotRepository);

            Assert.Equal(day, await service.GetSlotDateAsync(slot.Id));
            Assert.Null(await service.GetSlotDateAsync(Guid.NewGuid()));
            Assert.Null(await service.GetSlotDateAsync(Guid.Empty));
        }

        [Fact]
        public async Task BookSlotAsyncShouldReturnFalseWhenSlotAlreadyBooked()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;

            using var dbContext = new ApplicationDbContext(options);
            using var slotRepository = new EfDeletableEntityRepository<AvailabilitySlot>(dbContext);

            var existingBookingId = Guid.NewGuid();
            var slot = new AvailabilitySlot
            {
                StartTime = DateTime.Today.AddHours(9),
                EndTime = DateTime.Today.AddHours(10),
                IsBooked = true,
                BookingId = existingBookingId,
            };
            dbContext.AvailabilitySlots.Add(slot);
            await dbContext.SaveChangesAsync();

            var service = new AvailabilityService(slotRepository);
            var result = await service.BookSlotAsync(slot.Id, Guid.NewGuid());

            Assert.False(result);
            var slotInDb = dbContext.AvailabilitySlots.First(x => x.Id == slot.Id);
            Assert.Equal(existingBookingId, slotInDb.BookingId);
        }

        [Fact]
        public async Task BookSlotAsyncShouldReturnFalseWhenSlotIsBlocked()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;

            using var dbContext = new ApplicationDbContext(options);
            using var slotRepository = new EfDeletableEntityRepository<AvailabilitySlot>(dbContext);

            var slot = new AvailabilitySlot
            {
                StartTime = DateTime.Today.AddHours(9),
                EndTime = DateTime.Today.AddHours(10),
                IsBooked = false,
                IsBlocked = true,
            };
            dbContext.AvailabilitySlots.Add(slot);
            await dbContext.SaveChangesAsync();

            var service = new AvailabilityService(slotRepository);
            var result = await service.BookSlotAsync(slot.Id, Guid.NewGuid());

            Assert.False(result);
        }

        [Fact]
        public async Task BookSlotAsyncShouldReturnFalseWhenAnotherRequestWonTheConcurrencyRace()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;

            using var dbContext = new ApplicationDbContext(options);
            using var slotRepository = new EfDeletableEntityRepository<AvailabilitySlot>(dbContext);

            var slot = new AvailabilitySlot { StartTime = DateTime.Today.AddHours(9), EndTime = DateTime.Today.AddHours(10), IsBooked = false };
            dbContext.AvailabilitySlots.Add(slot);
            await dbContext.SaveChangesAsync();

            // Simulate another request having already updated this row (and thereby
            // bumped its RowVersion) between our read and our write, exactly as SQL
            // Server's auto-incrementing rowversion column would in production.
            dbContext.Entry(slot).Property(x => x.RowVersion).OriginalValue = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };

            var service = new AvailabilityService(slotRepository);
            var result = await service.BookSlotAsync(slot.Id, Guid.NewGuid());

            Assert.False(result);
        }

        [Fact]
        public async Task ReleaseSlotAsyncShouldReturnFalseWhenAnotherRequestWonTheConcurrencyRace()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;

            using var dbContext = new ApplicationDbContext(options);
            using var slotRepository = new EfDeletableEntityRepository<AvailabilitySlot>(dbContext);

            var slot = new AvailabilitySlot
            {
                StartTime = DateTime.Today.AddHours(9),
                EndTime = DateTime.Today.AddHours(10),
                IsBooked = true,
                BookingId = Guid.NewGuid(),
            };
            dbContext.AvailabilitySlots.Add(slot);
            await dbContext.SaveChangesAsync();

            // Simulate another request having already updated this row between our
            // read and our write, exactly as SQL Server's rowversion column would.
            dbContext.Entry(slot).Property(x => x.RowVersion).OriginalValue = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };

            var service = new AvailabilityService(slotRepository);
            var result = await service.ReleaseSlotAsync(slot.Id);

            Assert.False(result);
        }

        [Fact]
        public async Task GetAllSlotsForDateAsyncShouldNotGenerateCapacityWhenNoneExists()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;

            using var dbContext = new ApplicationDbContext(options);
            using var slotRepository = new EfDeletableEntityRepository<AvailabilitySlot>(dbContext);

            var service = new AvailabilityService(slotRepository);
            var slots = (await service.GetAllSlotsForDateAsync<AvailabilitySlot>(DateTime.Today.AddDays(1))).ToList();

            Assert.Empty(slots);
            Assert.Empty(dbContext.AvailabilitySlots);
        }

        // The booking page's slots for a day leave out the hours already gone, so a customer
        // cannot pick a time that has passed. Today's slot here starts at midnight, which has
        // always passed by the time the test runs.
        [Fact]
        public async Task GetAllSlotsForDateAsyncShouldLeaveOutSlotsThatHaveAlreadyStarted()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;

            using var dbContext = new ApplicationDbContext(options);
            using var slotRepository = new EfDeletableEntityRepository<AvailabilitySlot>(dbContext);

            var tomorrow = DateTime.Today.AddDays(1);
            dbContext.AvailabilitySlots.Add(new AvailabilitySlot { StartTime = DateTime.Today, EndTime = DateTime.Today.AddHours(1) });
            dbContext.AvailabilitySlots.Add(new AvailabilitySlot { StartTime = tomorrow.AddHours(9), EndTime = tomorrow.AddHours(10) });
            await dbContext.SaveChangesAsync();

            var service = new AvailabilityService(slotRepository);

            Assert.Empty(await service.GetAllSlotsForDateAsync<AvailabilitySlot>(DateTime.Today));
            Assert.Single(await service.GetAllSlotsForDateAsync<AvailabilitySlot>(tomorrow));
        }

        [Fact]
        public async Task GenerateSlotsForRangeAsyncShouldCreateBusinessHoursWithNoTechniciansInTheDatabase()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;

            using var dbContext = new ApplicationDbContext(options);
            using var slotRepository = new EfDeletableEntityRepository<AvailabilitySlot>(dbContext);

            // Deliberately no Technician rows: capacity no longer depends on anyone existing.
            // This used to return early and silently generate nothing at all.
            var monday = NextWeekday(DayOfWeek.Monday);
            var service = new AvailabilityService(slotRepository);
            await service.GenerateSlotsForRangeAsync(monday, monday);

            var slots = dbContext.AvailabilitySlots.OrderBy(x => x.StartTime).ToList();

            Assert.Equal(8, slots.Count);
            Assert.Equal(monday.AddHours(9), slots.First().StartTime);
            Assert.Equal(monday.AddHours(17), slots.Last().EndTime);
        }

        [Fact]
        public async Task GenerateSlotsForRangeAsyncShouldSkipSundaysAndNotDuplicateExistingDays()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;

            using var dbContext = new ApplicationDbContext(options);
            using var slotRepository = new EfDeletableEntityRepository<AvailabilitySlot>(dbContext);

            var sunday = NextWeekday(DayOfWeek.Sunday);
            var monday = sunday.AddDays(1);

            var service = new AvailabilityService(slotRepository);
            await service.GenerateSlotsForRangeAsync(sunday, monday);
            await service.GenerateSlotsForRangeAsync(sunday, monday);

            var slots = dbContext.AvailabilitySlots.ToList();

            Assert.Equal(8, slots.Count);
            Assert.All(slots, slot => Assert.Equal(monday.Date, slot.StartTime.Date));
        }

        private static DateTime NextWeekday(DayOfWeek dayOfWeek)
        {
            var date = DateTime.Today.AddDays(1);
            while (date.DayOfWeek != dayOfWeek)
            {
                date = date.AddDays(1);
            }

            return date;
        }
    }
}
