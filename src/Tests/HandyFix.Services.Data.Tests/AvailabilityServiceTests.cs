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
            using var technicianRepository = new EfDeletableEntityRepository<Technician>(dbContext);

            var slot = new AvailabilitySlot { StartTime = DateTime.Today.AddHours(9), EndTime = DateTime.Today.AddHours(10), IsBooked = false };
            dbContext.AvailabilitySlots.Add(slot);
            await dbContext.SaveChangesAsync();

            var service = new AvailabilityService(slotRepository, technicianRepository);
            var bookingId = Guid.NewGuid();
            var result = await service.BookSlotAsync(slot.Id, bookingId);

            Assert.True(result);
            var updatedSlot = dbContext.AvailabilitySlots.First(x => x.Id == slot.Id);
            Assert.True(updatedSlot.IsBooked);
            Assert.Equal(bookingId, updatedSlot.BookingId);
        }

        [Fact]
        public async Task GetAvailableDatesAsyncShouldReturnCorrectDates()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;
            
            using var dbContext = new ApplicationDbContext(options);
            using var slotRepository = new EfDeletableEntityRepository<AvailabilitySlot>(dbContext);
            using var technicianRepository = new EfDeletableEntityRepository<Technician>(dbContext);

            // Add slots on different dates
            var date1 = DateTime.Today.AddDays(1);
            var date2 = DateTime.Today.AddDays(2);
            
            dbContext.AvailabilitySlots.Add(new AvailabilitySlot { StartTime = date1.AddHours(9), EndTime = date1.AddHours(10), IsBooked = false });
            dbContext.AvailabilitySlots.Add(new AvailabilitySlot { StartTime = date1.AddHours(10), EndTime = date1.AddHours(11), IsBooked = false });
            dbContext.AvailabilitySlots.Add(new AvailabilitySlot { StartTime = date2.AddHours(9), EndTime = date2.AddHours(10), IsBooked = false });
            dbContext.AvailabilitySlots.Add(new AvailabilitySlot { StartTime = date2.AddHours(10), EndTime = date2.AddHours(11), IsBooked = true });
            await dbContext.SaveChangesAsync();

            var service = new AvailabilityService(slotRepository, technicianRepository);
            var dates = (await service.GetAvailableDatesAsync()).ToList();

            Assert.Equal(2, dates.Count);
            Assert.Contains(date1.Date, dates);
            Assert.Contains(date2.Date, dates);
        }
    }
}
