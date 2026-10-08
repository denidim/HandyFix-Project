namespace HandyFix.Services.Data.Tests
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Data;
    using HandyFix.Data.Models;
    using HandyFix.Data.Seeding;

    using Microsoft.EntityFrameworkCore;

    using Xunit;

    public class TechniciansSeederTests
    {
        // Deleting a technician is a soft delete. The seeder counted live rows only, so a table
        // holding nothing but the deleted placeholder looked empty and got the placeholder again
        // (PROJECT_STATE.md Section 3cb).
        [Fact]
        public async Task SeedAsyncShouldNotBringBackThePlaceholderAnAdminDeleted()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;

            using var dbContext = new ApplicationDbContext(options);

            await new TechniciansSeeder().SeedAsync(dbContext, null);
            await dbContext.SaveChangesAsync();

            Technician placeholder = dbContext.Technicians.Single();
            placeholder.IsDeleted = true;
            placeholder.DeletedOn = DateTime.UtcNow;
            await dbContext.SaveChangesAsync();

            await new TechniciansSeeder().SeedAsync(dbContext, null);
            await dbContext.SaveChangesAsync();

            Assert.Empty(dbContext.Technicians);
            Assert.Single(dbContext.Technicians.IgnoreQueryFilters());
        }
    }
}
