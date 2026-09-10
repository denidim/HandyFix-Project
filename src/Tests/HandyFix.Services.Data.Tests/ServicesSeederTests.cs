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

    public class ServicesSeederTests
    {
        // Guards the bug recorded in PROJECT_STATE.md Section 3av: Section 3ao moved this service
        // from Plumbing to Handyman in the seeder, but the upsert only synced price/duration, so an
        // already-seeded database kept listing it under Plumbing at the Handyman price.
        [Fact]
        public async Task SeedAsyncShouldMoveAnExistingServiceToItsSeededCategory()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;

            using var dbContext = new ApplicationDbContext(options);

            var plumbing = new ServiceCategory { Name = "Plumbing", Slug = "plumbing", Description = "Plumbing" };
            var handyman = new ServiceCategory { Name = "Handyman", Slug = "handyman", Description = "Handyman" };
            var building = new ServiceCategory { Name = "Small Building & Refurbishments", Slug = "small-building-works", Description = "Building" };
            dbContext.ServiceCategories.AddRange(plumbing, handyman, building);

            dbContext.Services.Add(new Service
            {
                Name = "Bath & Shower Screen Fitting",
                Slug = "bath-shower-screen-fitting",
                Description = "Seeded before the category move.",
                BasePrice = 90.00m,
                EstimatedDurationMinutes = 60,
                CategoryId = plumbing.Id,
                IsActive = true,
            });
            await dbContext.SaveChangesAsync();

            await new ServicesSeeder().SeedAsync(dbContext, null);

            var service = dbContext.Services.Single(s => s.Name == "Bath & Shower Screen Fitting");
            Assert.Equal(handyman.Id, service.CategoryId);
            Assert.Equal(60.00m, service.BasePrice);
        }
    }
}
