namespace HandyFix.Services.Data.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Data;
    using HandyFix.Data.Models;
    using HandyFix.Data.Repositories;
    using HandyFix.Services.Data.Availability;
    using HandyFix.Services.Data.Bookings;
    using HandyFix.Services.Messaging;
    using HandyFix.Web.ViewModels.Booking;

    using Microsoft.Data.Sqlite;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Diagnostics;

    using Moq;

    using Xunit;

    public class BookingsServiceTests
    {
        [Fact]
        public async Task CreateBookingAsyncShouldCreateBookingCorrectly()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            
            using var dbContext = new ApplicationDbContext(options);
            using var bookingRepo = new EfDeletableEntityRepository<Booking>(dbContext);
            using var bookingStatusRepo = new EfDeletableEntityRepository<BookingStatus>(dbContext);
            using var slotRepo = new EfDeletableEntityRepository<AvailabilitySlot>(dbContext);
            using var serviceRepo = new EfDeletableEntityRepository<Service>(dbContext);
            using var bookingServiceRepo = new EfDeletableEntityRepository<BookingService>(dbContext);
            using var bookingImageRepo = new EfDeletableEntityRepository<BookingImage>(dbContext);
            using var technicianRepo = new EfDeletableEntityRepository<Technician>(dbContext);

            var availabilityService = new AvailabilityService(slotRepo, technicianRepo);
            var dbQueryRunner = new DbQueryRunner(dbContext);

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
                availabilityService,
                dbQueryRunner,
                emailSenderMock.Object);

            var model = new BookingInputModel
            {
                CustomerFirstName = "John",
                CustomerLastName = "Doe",
                Email = "john@example.com",
                PhoneNumber = "07123456789",
                Address = "12 Main Rd, Sutton",
                ProblemDescription = "Leaking kitchen sink pipe",
                SlotId = slot.Id,
                ServiceId = serviceEntity.Id,
            };

            var booking = await bookingsService.CreateBookingAsync(
                model,
                new List<string>());

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

        [Fact]
        public async Task CreateBookingAsyncShouldRejectAndRollBackWhenSlotAlreadyBooked()
        {
            // The rollback here depends on a real, honored transaction. EF Core's
            // InMemory provider silently ignores transactions (see
            // InMemoryEventId.TransactionIgnoredWarning), so this test uses an
            // in-memory SQLite database instead, which supports them for real.
            using var connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();

            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connection)
                .Options;

            using var dbContext = new ApplicationDbContext(options);
            await dbContext.Database.EnsureCreatedAsync();
            using var bookingRepo = new EfDeletableEntityRepository<Booking>(dbContext);
            using var bookingStatusRepo = new EfDeletableEntityRepository<BookingStatus>(dbContext);
            using var slotRepo = new EfDeletableEntityRepository<AvailabilitySlot>(dbContext);
            using var serviceRepo = new EfDeletableEntityRepository<Service>(dbContext);
            using var bookingImageRepo = new EfDeletableEntityRepository<BookingImage>(dbContext);
            using var technicianRepo = new EfDeletableEntityRepository<Technician>(dbContext);

            var availabilityService = new AvailabilityService(slotRepo, technicianRepo);
            var dbQueryRunner = new DbQueryRunner(dbContext);
            var emailSenderMock = new Mock<IEmailSender>();

            var pendingStatus = new BookingStatus { Id = Guid.NewGuid(), Name = "Pending" };
            dbContext.BookingStatuses.Add(pendingStatus);

            var category = new ServiceCategory { Id = Guid.NewGuid(), Name = "Plumbing", Description = "leak repairs", Slug = "plumbing" };
            dbContext.ServiceCategories.Add(category);

            var serviceEntity = new Service
            {
                Id = Guid.NewGuid(),
                Name = "Leak Fix",
                Description = "Repair leak",
                Slug = "leak-fix",
                BasePrice = 80.00m,
                CategoryId = category.Id,
            };
            dbContext.Services.Add(serviceEntity);

            // Slot is already booked by another customer
            var existingBooking = new Booking
            {
                CustomerFirstName = "Existing",
                CustomerLastName = "Customer",
                Email = "existing@example.com",
                PhoneNumber = "07123456781",
                Address = "1 Other Rd, Sutton",
                ProblemDescription = "An unrelated, already-confirmed job",
                StatusId = pendingStatus.Id,
            };
            dbContext.Bookings.Add(existingBooking);

            var slot = new AvailabilitySlot
            {
                StartTime = DateTime.Today.AddHours(9),
                EndTime = DateTime.Today.AddHours(10),
                IsBooked = true,
                BookingId = existingBooking.Id,
            };
            dbContext.AvailabilitySlots.Add(slot);
            await dbContext.SaveChangesAsync();

            var bookingsService = new BookingsService(
                bookingRepo,
                serviceRepo,
                slotRepo,
                bookingStatusRepo,
                bookingImageRepo,
                availabilityService,
                dbQueryRunner,
                emailSenderMock.Object);

            var model = new BookingInputModel
            {
                CustomerFirstName = "Jane",
                CustomerLastName = "Smith",
                Email = "jane@example.com",
                PhoneNumber = "07123456780",
                Address = "5 Other Rd, Sutton",
                ProblemDescription = "Blocked kitchen drain",
                SlotId = slot.Id,
                ServiceId = serviceEntity.Id,
            };

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => bookingsService.CreateBookingAsync(model, new List<string>()));

            // The slot must still point to the original booking, untouched
            var slotInDb = dbContext.AvailabilitySlots.First(x => x.Id == slot.Id);
            Assert.Equal(existingBooking.Id, slotInDb.BookingId);

            // No orphaned booking should have been left behind by the failed attempt
            Assert.Empty(dbContext.Bookings.Where(x => x.Email == "jane@example.com"));

            emailSenderMock.Verify(
                x => x.SendEmailAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    null),
                Times.Never);
        }
    }
}
