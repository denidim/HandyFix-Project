namespace HandyFix.Services.Data.Tests
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Common;
    using HandyFix.Data;
    using HandyFix.Data.Models;
    using HandyFix.Data.Seeding;

    using Microsoft.EntityFrameworkCore;

    using Xunit;

    public class TechniciansSeederTests
    {
        // The live site's database is a new one. Until Section 3cd a new database started with
        // "John Doe" on a made-up number, active, so an admin could have assigned him to a real
        // booking and the customer been emailed his name and that number.
        [Fact]
        public async Task SeedAsyncShouldStartANewDatabaseWithTheLaunchTechnicianOnTheBusinessNumber()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;

            using var dbContext = new ApplicationDbContext(options);

            await new TechniciansSeeder().SeedAsync(dbContext, null);
            await dbContext.SaveChangesAsync();

            Technician technician = dbContext.Technicians.Single();
            Assert.Equal("Zapryan", technician.FirstName);
            Assert.Null(technician.LastName);
            Assert.Equal(GlobalConstants.BusinessPhone, technician.PhoneNumber);
            Assert.True(technician.IsActive);
        }

        // Deleting a technician is a soft delete. The seeder counted live rows only, so a table
        // holding nothing but a deleted technician looked empty and got the seeded one again
        // (PROJECT_STATE.md Section 3cb).
        [Fact]
        public async Task SeedAsyncShouldNotBringBackTheTechnicianAnAdminDeleted()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;

            using var dbContext = new ApplicationDbContext(options);

            await new TechniciansSeeder().SeedAsync(dbContext, null);
            await dbContext.SaveChangesAsync();

            Technician seeded = dbContext.Technicians.Single();
            seeded.IsDeleted = true;
            seeded.DeletedOn = DateTime.UtcNow;
            await dbContext.SaveChangesAsync();

            await new TechniciansSeeder().SeedAsync(dbContext, null);
            await dbContext.SaveChangesAsync();

            Assert.Empty(dbContext.Technicians);
            Assert.Single(dbContext.Technicians.IgnoreQueryFilters());
        }
    }
}
