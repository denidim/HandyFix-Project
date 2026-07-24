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

            await Assert.ThrowsAsync<SlotUnavailableException>(
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

        [Fact]
        public async Task RescheduleBookingAsyncShouldReleaseOldSlotAndClaimNewOne()
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
            using var bookingImageRepo = new EfDeletableEntityRepository<BookingImage>(dbContext);
            using var technicianRepo = new EfDeletableEntityRepository<Technician>(dbContext);

            var availabilityService = new AvailabilityService(slotRepo, technicianRepo);
            var dbQueryRunner = new DbQueryRunner(dbContext);
            var emailSenderMock = new Mock<IEmailSender>();

            var pendingStatus = new BookingStatus { Id = Guid.NewGuid(), Name = "Pending" };
            dbContext.BookingStatuses.Add(pendingStatus);

            var technician = new Technician { FirstName = "Alex", LastName = "Smith", PhoneNumber = "07000000000" };
            dbContext.Technicians.Add(technician);

            var booking = new Booking
            {
                CustomerFirstName = "John",
                CustomerLastName = "Doe",
                Email = "john@example.com",
                PhoneNumber = "07123456789",
                Address = "12 Main Rd, Sutton",
                ProblemDescription = "Leaking kitchen sink pipe",
                StatusId = pendingStatus.Id,
            };
            dbContext.Bookings.Add(booking);

            var oldSlot = new AvailabilitySlot
            {
                StartTime = DateTime.Today.AddHours(9),
                EndTime = DateTime.Today.AddHours(10),
                IsBooked = true,
                BookingId = booking.Id,
            };
            var newSlot = new AvailabilitySlot
            {
                StartTime = DateTime.Today.AddDays(1).AddHours(11),
                EndTime = DateTime.Today.AddDays(1).AddHours(12),
                IsBooked = false,
                TechnicianId = technician.Id,
            };
            dbContext.AvailabilitySlots.Add(oldSlot);
            dbContext.AvailabilitySlots.Add(newSlot);
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

            await bookingsService.RescheduleBookingAsync(booking.Id, newSlot.Id);

            var oldSlotInDb = dbContext.AvailabilitySlots.First(x => x.Id == oldSlot.Id);
            var newSlotInDb = dbContext.AvailabilitySlots.First(x => x.Id == newSlot.Id);
            var bookingInDb = dbContext.Bookings.First(x => x.Id == booking.Id);

            Assert.False(oldSlotInDb.IsBooked);
            Assert.Null(oldSlotInDb.BookingId);
            Assert.True(newSlotInDb.IsBooked);
            Assert.Equal(booking.Id, newSlotInDb.BookingId);
            Assert.Equal(technician.Id, bookingInDb.TechnicianId);
        }

        [Fact]
        public async Task RescheduleBookingAsyncShouldRollBackAndKeepOldSlotWhenNewSlotAlreadyBooked()
        {
            // As with the create-booking rollback test, this depends on a real,
            // honored transaction, which EF Core's InMemory provider does not
            // support, so this test uses an in-memory SQLite database instead.
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

            var booking = new Booking
            {
                CustomerFirstName = "John",
                CustomerLastName = "Doe",
                Email = "john@example.com",
                PhoneNumber = "07123456789",
                Address = "12 Main Rd, Sutton",
                ProblemDescription = "Leaking kitchen sink pipe",
                StatusId = pendingStatus.Id,
            };
            dbContext.Bookings.Add(booking);

            var otherBooking = new Booking
            {
                CustomerFirstName = "Existing",
                CustomerLastName = "Customer",
                Email = "existing@example.com",
                PhoneNumber = "07123456781",
                Address = "1 Other Rd, Sutton",
                ProblemDescription = "An unrelated, already-confirmed job",
                StatusId = pendingStatus.Id,
            };
            dbContext.Bookings.Add(otherBooking);

            var oldSlot = new AvailabilitySlot
            {
                StartTime = DateTime.Today.AddHours(9),
                EndTime = DateTime.Today.AddHours(10),
                IsBooked = true,
                BookingId = booking.Id,
            };
            var newSlot = new AvailabilitySlot
            {
                StartTime = DateTime.Today.AddDays(1).AddHours(11),
                EndTime = DateTime.Today.AddDays(1).AddHours(12),
                IsBooked = true,
                BookingId = otherBooking.Id,
            };
            dbContext.AvailabilitySlots.Add(oldSlot);
            dbContext.AvailabilitySlots.Add(newSlot);
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

            await Assert.ThrowsAsync<SlotUnavailableException>(
                () => bookingsService.RescheduleBookingAsync(booking.Id, newSlot.Id));

            // Read back through a fresh DbContext on the same connection: the acting
            // dbContext's change tracker still holds the in-memory values it set on
            // oldSlot before the rollback, which a tracked query would just return
            // as-is instead of reflecting what was actually rolled back in the database.
            using var verifyContext = new ApplicationDbContext(options);
            var oldSlotInDb = await verifyContext.AvailabilitySlots.AsNoTracking().FirstAsync(x => x.Id == oldSlot.Id);
            var newSlotInDb = await verifyContext.AvailabilitySlots.AsNoTracking().FirstAsync(x => x.Id == newSlot.Id);

            // The old slot must remain exactly as it was: the failed reschedule
            // must not have left the booking stranded with no appointment at all.
            Assert.True(oldSlotInDb.IsBooked);
            Assert.Equal(booking.Id, oldSlotInDb.BookingId);
            Assert.Equal(otherBooking.Id, newSlotInDb.BookingId);
        }

        [Fact]
        public async Task ReleaseAbandonedBookingsAsyncShouldAbandonStaleBookingsAndFreeSlots()
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
            using var bookingImageRepo = new EfDeletableEntityRepository<BookingImage>(dbContext);
            using var technicianRepo = new EfDeletableEntityRepository<Technician>(dbContext);

            var availabilityService = new AvailabilityService(slotRepo, technicianRepo);
            var dbQueryRunner = new DbQueryRunner(dbContext);
            var emailSenderMock = new Mock<IEmailSender>();

            var pendingStatus = new BookingStatus { Id = Guid.NewGuid(), Name = "Pending" };
            var approvedStatus = new BookingStatus { Id = Guid.NewGuid(), Name = "Approved" };
            var abandonedStatus = new BookingStatus { Id = Guid.NewGuid(), Name = "Abandoned" };
            dbContext.BookingStatuses.AddRange(pendingStatus, approvedStatus, abandonedStatus);

            // A ghost checkout: created 20 minutes ago, deposit never paid.
            var staleBooking = new Booking
            {
                CustomerFirstName = "Stale",
                CustomerLastName = "Ghost",
                Email = "stale@example.com",
                PhoneNumber = "07000000001",
                Address = "1 Ghost Rd, Sutton",
                ProblemDescription = "Never completed the deposit payment",
                StatusId = pendingStatus.Id,
            };

            // A booking created seconds ago, still well within the grace period.
            var freshBooking = new Booking
            {
                CustomerFirstName = "Fresh",
                CustomerLastName = "Customer",
                Email = "fresh@example.com",
                PhoneNumber = "07000000002",
                Address = "2 Fresh Rd, Sutton",
                ProblemDescription = "Just started checking out",
                StatusId = pendingStatus.Id,
            };

            // An old booking that already had its deposit confirmed; must be left alone.
            var paidBooking = new Booking
            {
                CustomerFirstName = "Paid",
                CustomerLastName = "Customer",
                Email = "paid@example.com",
                PhoneNumber = "07000000003",
                Address = "3 Paid Rd, Sutton",
                ProblemDescription = "Already confirmed and paid",
                StatusId = approvedStatus.Id,
            };
            dbContext.Bookings.AddRange(staleBooking, freshBooking, paidBooking);

            var staleSlot = new AvailabilitySlot { StartTime = DateTime.Today.AddHours(9), EndTime = DateTime.Today.AddHours(10), IsBooked = true, BookingId = staleBooking.Id };
            var freshSlot = new AvailabilitySlot { StartTime = DateTime.Today.AddHours(11), EndTime = DateTime.Today.AddHours(12), IsBooked = true, BookingId = freshBooking.Id };
            var paidSlot = new AvailabilitySlot { StartTime = DateTime.Today.AddHours(13), EndTime = DateTime.Today.AddHours(14), IsBooked = true, BookingId = paidBooking.Id };
            dbContext.AvailabilitySlots.AddRange(staleSlot, freshSlot, paidSlot);
            await dbContext.SaveChangesAsync();

            // Back-date the stale booking's CreatedOn past the 15-minute grace period.
            staleBooking.CreatedOn = DateTime.UtcNow.AddMinutes(-20);
            paidBooking.CreatedOn = DateTime.UtcNow.AddMinutes(-20);
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

            var releasedCount = await bookingsService.ReleaseAbandonedBookingsAsync(TimeSpan.FromMinutes(15));

            Assert.Equal(1, releasedCount);

            var staleBookingInDb = dbContext.Bookings.First(x => x.Id == staleBooking.Id);
            var freshBookingInDb = dbContext.Bookings.First(x => x.Id == freshBooking.Id);
            var paidBookingInDb = dbContext.Bookings.First(x => x.Id == paidBooking.Id);

            Assert.Equal(abandonedStatus.Id, staleBookingInDb.StatusId);
            Assert.Equal(pendingStatus.Id, freshBookingInDb.StatusId);
            Assert.Equal(approvedStatus.Id, paidBookingInDb.StatusId);

            var staleSlotInDb = dbContext.AvailabilitySlots.First(x => x.Id == staleSlot.Id);
            var freshSlotInDb = dbContext.AvailabilitySlots.First(x => x.Id == freshSlot.Id);
            var paidSlotInDb = dbContext.AvailabilitySlots.First(x => x.Id == paidSlot.Id);

            Assert.False(staleSlotInDb.IsBooked);
            Assert.Null(staleSlotInDb.BookingId);
            Assert.True(freshSlotInDb.IsBooked);
            Assert.True(paidSlotInDb.IsBooked);
        }
    }
}
