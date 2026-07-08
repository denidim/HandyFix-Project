namespace HandyFix.Services.Data.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Data;
    using HandyFix.Data.Models;
    using HandyFix.Data.Repositories;
    using HandyFix.Services.Data.Bookings;
    using HandyFix.Services.Messaging;

    using Microsoft.EntityFrameworkCore;

    using Moq;

    using Xunit;

    public class BookingsServiceTests
    {
        [Fact]
        public async Task CreateBookingAsyncShouldCreateBookingCorrectly()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;
            
            using var dbContext = new ApplicationDbContext(options);
            using var bookingRepo = new EfDeletableEntityRepository<Booking>(dbContext);
            using var bookingStatusRepo = new EfDeletableEntityRepository<BookingStatus>(dbContext);
            using var slotRepo = new EfDeletableEntityRepository<AvailabilitySlot>(dbContext);
            using var serviceRepo = new EfDeletableEntityRepository<Service>(dbContext);
            using var bookingServiceRepo = new EfDeletableEntityRepository<BookingService>(dbContext);
            using var bookingImageRepo = new EfDeletableEntityRepository<BookingImage>(dbContext);
            using var technicianRepo = new EfDeletableEntityRepository<Technician>(dbContext);

            var emailSenderMock = new Mock<IEmailSender>();

            // Seed required statuses
            var pendingStatus = new BookingStatus { Id = Guid.NewGuid(), Name = "Pending" };
            dbContext.BookingStatuses.Add(pendingStatus);

            // Seed a service
            var category = new ServiceCategory { Id = Guid.NewGuid(), Name = "Plumbing", Description = "leak repairs", Slug = "plumbing" };
            dbContext.ServiceCategories.Add(category);

            var serviceEntity = new Service 
            { 
                Id = Guid.NewGuid(), 
                Name = "Leak Fix", 
                Description = "Repair leak", 
                Slug = "leak-fix", 
                BasePrice = 80.00m,
                CategoryId = category.Id
            };
            dbContext.Services.Add(serviceEntity);

            // Seed a slot
            var slot = new AvailabilitySlot { StartTime = DateTime.Today.AddHours(9), EndTime = DateTime.Today.AddHours(10), IsBooked = false };
            dbContext.AvailabilitySlots.Add(slot);
            await dbContext.SaveChangesAsync();

            var bookingsService = new BookingsService(
                bookingRepo,
                serviceRepo,
                slotRepo,
                bookingStatusRepo,
                bookingImageRepo,
                emailSenderMock.Object);

            var booking = await bookingsService.CreateBookingAsync(
                "John",
                "Doe",
                "john@example.com",
                "07123456789",
                "12 Main Rd, Sutton",
                "Leaking kitchen sink pipe",
                slot.Id,
                new[] { serviceEntity.Id });

            Assert.NotNull(booking);
            Assert.Equal("John", booking.CustomerFirstName);
            Assert.Equal("Doe", booking.CustomerLastName);
            Assert.Equal("john@example.com", booking.Email);
            Assert.Equal("07123456789", booking.PhoneNumber);
            Assert.Equal("12 Main Rd, Sutton", booking.Address);
            Assert.Equal("Leaking kitchen sink pipe", booking.ProblemDescription);
            Assert.Equal(pendingStatus.Id, booking.StatusId);

            // Verify slot is marked booked
            var slotInDb = dbContext.AvailabilitySlots.First(x => x.Id == slot.Id);
            Assert.True(slotInDb.IsBooked);

            // Verify email receipt was sent
            emailSenderMock.Verify(
                x => x.SendEmailAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    "john@example.com",
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    null),
                Times.Once);
        }
    }
}
