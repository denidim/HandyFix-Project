namespace HandyFix.Services.Data.Tests
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Data;
    using HandyFix.Data.Models;
    using HandyFix.Data.Repositories;
    using HandyFix.Services.Data.Technicians;
    using HandyFix.Web.ViewModels.Administration.Technicians;

    using Microsoft.EntityFrameworkCore;

    using Xunit;

    public class TechniciansServiceTests
    {
        [Fact]
        public async Task CreateAsyncShouldPersistTheTechnician()
        {
            using var dbContext = NewDbContext();
            using var techniciansRepo = new EfDeletableEntityRepository<Technician>(dbContext);
            using var bookingsRepo = new EfDeletableEntityRepository<Booking>(dbContext);

            var service = new TechniciansService(techniciansRepo, bookingsRepo);

            var id = await service.CreateAsync(new TechnicianAdminInputModel
            {
                FirstName = "  Zapryan  ",
                LastName = "Petrov",
                PhoneNumber = " 07700900123 ",
                IsActive = true,
            });

            var technician = dbContext.Technicians.Single(x => x.Id == id);

            Assert.Equal("Zapryan", technician.FirstName);
            Assert.Equal("Petrov", technician.LastName);
            Assert.Equal("07700900123", technician.PhoneNumber);
            Assert.True(technician.IsActive);
        }

        [Fact]
        public async Task UpdateAsyncShouldChangeDetailsAndActiveFlag()
        {
            using var dbContext = NewDbContext();
            using var techniciansRepo = new EfDeletableEntityRepository<Technician>(dbContext);
            using var bookingsRepo = new EfDeletableEntityRepository<Booking>(dbContext);

            var technician = new Technician { FirstName = "Alex", LastName = "Smith", PhoneNumber = "07000000000" };
            dbContext.Technicians.Add(technician);
            await dbContext.SaveChangesAsync();

            var service = new TechniciansService(techniciansRepo, bookingsRepo);

            await service.UpdateAsync(technician.Id, new TechnicianAdminInputModel
            {
                Id = technician.Id,
                FirstName = "Alexander",
                LastName = "Smith",
                PhoneNumber = "07111111111",
                IsActive = false,
            });

            var updated = dbContext.Technicians.Single(x => x.Id == technician.Id);

            Assert.Equal("Alexander", updated.FirstName);
            Assert.Equal("07111111111", updated.PhoneNumber);
            Assert.False(updated.IsActive);
        }

        [Fact]
        public async Task GetAllAsyncShouldReturnActiveOnlyWhenRequested()
        {
            using var dbContext = NewDbContext();
            using var techniciansRepo = new EfDeletableEntityRepository<Technician>(dbContext);
            using var bookingsRepo = new EfDeletableEntityRepository<Booking>(dbContext);

            dbContext.Technicians.Add(new Technician { FirstName = "Active", LastName = "One", PhoneNumber = "07000000001", IsActive = true });
            dbContext.Technicians.Add(new Technician { FirstName = "Retired", LastName = "Two", PhoneNumber = "07000000002", IsActive = false });
            await dbContext.SaveChangesAsync();

            var service = new TechniciansService(techniciansRepo, bookingsRepo);

            var all = (await service.GetAllAsync<TechnicianAdminListViewModel>()).ToList();
            var activeOnly = (await service.GetAllAsync<TechnicianAdminListViewModel>(activeOnly: true)).ToList();

            Assert.Equal(2, all.Count);
            Assert.Single(activeOnly);
            Assert.Equal("Active One", activeOnly[0].FullName);
        }

        [Fact]
        public async Task GetAssignableAsyncShouldIncludeTheCurrentlyAssignedTechnicianEvenWhenInactive()
        {
            using var dbContext = NewDbContext();
            using var techniciansRepo = new EfDeletableEntityRepository<Technician>(dbContext);
            using var bookingsRepo = new EfDeletableEntityRepository<Booking>(dbContext);

            var active = new Technician { FirstName = "Active", LastName = "One", PhoneNumber = "07000000001", IsActive = true };
            var retired = new Technician { FirstName = "Retired", LastName = "Two", PhoneNumber = "07000000002", IsActive = false };
            dbContext.Technicians.Add(active);
            dbContext.Technicians.Add(retired);
            await dbContext.SaveChangesAsync();

            var service = new TechniciansService(techniciansRepo, bookingsRepo);

            var withoutCurrent = (await service.GetAssignableAsync<TechnicianOptionViewModel>()).ToList();
            var withCurrent = (await service.GetAssignableAsync<TechnicianOptionViewModel>(retired.Id)).ToList();

            // Without this, opening a booking assigned to a since-deactivated technician would
            // render a picker that doesn't contain them - and quietly unassign on the next save.
            Assert.Single(withoutCurrent);
            Assert.Equal(2, withCurrent.Count);
            Assert.Contains(withCurrent, t => t.Id == retired.Id);
        }

        [Fact]
        public async Task DeleteAsyncShouldRemoveATechnicianWithNoBookings()
        {
            using var dbContext = NewDbContext();
            using var techniciansRepo = new EfDeletableEntityRepository<Technician>(dbContext);
            using var bookingsRepo = new EfDeletableEntityRepository<Booking>(dbContext);

            var technician = new Technician { FirstName = "Mistake", LastName = "Entry", PhoneNumber = "07000000003" };
            dbContext.Technicians.Add(technician);
            await dbContext.SaveChangesAsync();

            var service = new TechniciansService(techniciansRepo, bookingsRepo);
            var deleted = await service.DeleteAsync(technician.Id);

            Assert.True(deleted);
            Assert.Empty(await service.GetAllAsync<TechnicianAdminListViewModel>());
        }

        [Fact]
        public async Task DeleteAsyncShouldRefuseWhenTheTechnicianHasBookings()
        {
            using var dbContext = NewDbContext();
            using var techniciansRepo = new EfDeletableEntityRepository<Technician>(dbContext);
            using var bookingsRepo = new EfDeletableEntityRepository<Booking>(dbContext);

            var status = new BookingStatus { Id = Guid.NewGuid(), Name = "Approved" };
            dbContext.BookingStatuses.Add(status);

            var technician = new Technician { FirstName = "Busy", LastName = "Tech", PhoneNumber = "07000000004" };
            dbContext.Technicians.Add(technician);

            dbContext.Bookings.Add(new Booking
            {
                CustomerFirstName = "John",
                CustomerLastName = "Doe",
                Email = "john@example.com",
                PhoneNumber = "07123456789",
                Address = "12 Main Rd",
                ProblemDescription = "Leaking tap",
                StatusId = status.Id,
                TechnicianId = technician.Id,
            });
            await dbContext.SaveChangesAsync();

            var service = new TechniciansService(techniciansRepo, bookingsRepo);
            var deleted = await service.DeleteAsync(technician.Id);

            // Deleting would leave that booking pointing at a row the global query filter hides,
            // so its assignment would silently read as unassigned. Deactivation is the way out.
            Assert.False(deleted);
            Assert.Single(await service.GetAllAsync<TechnicianAdminListViewModel>());
        }

        private static ApplicationDbContext NewDbContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            return new ApplicationDbContext(options);
        }
    }
}
