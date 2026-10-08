namespace HandyFix.Services.Data.Tests
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Data;
    using HandyFix.Data.Models;
    using HandyFix.Data.Seeding;

    using Microsoft.Data.Sqlite;
    using Microsoft.EntityFrameworkCore;

    using Xunit;

    // The catalogue is seeded into an empty table and never touched again: from then on it is the
    // admin's (PROJECT_STATE.md Section 3cb). These run on Sqlite, not InMemory, because the fault
    // they guard was a unique-index violation, and InMemory enforces no indexes.
    public sealed class ServicesSeederTests : IDisposable
    {
        private readonly SqliteConnection connection;
        private readonly ApplicationDbContext dbContext;

        public ServicesSeederTests()
        {
            this.connection = new SqliteConnection("DataSource=:memory:");
            this.connection.Open();

            this.dbContext = new ApplicationDbContext(
                new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(this.connection).Options);
            this.dbContext.Database.EnsureCreated();
        }

        public void Dispose()
        {
            this.dbContext.Dispose();
            this.connection.Dispose();
        }

        [Fact]
        public async Task SeedAsyncShouldPutTheWholeCatalogueIntoAnEmptyTable()
        {
            await this.SeedAsync();

            Assert.Equal(37, this.dbContext.Services.Count());
            Assert.Equal(
                new[] { "general-handyman-call-out", "general-plumbing-maintenance" },
                this.dbContext.Services.Where(s => s.DisplayOrder == 0).Select(s => s.Slug).OrderBy(s => s));
            Assert.Equal(4, this.dbContext.Services.Count(s => s.IsPopular));
            Assert.Equal(37, this.dbContext.ServiceImages.Count());
        }

        // An admin deleting a seeded service used to stop the site from starting. The delete is a
        // soft one, the seeder could not see the row, and its insert of the "missing" service hit
        // the unique index on Slug, inside Program.Configure.
        [Fact]
        public async Task SeedAsyncShouldNotFailOrBringBackAServiceAnAdminDeleted()
        {
            await this.SeedAsync();

            Service deleted = this.dbContext.Services.Single(s => s.Slug == "gutter-clearing");
            deleted.IsDeleted = true;
            deleted.DeletedOn = DateTime.UtcNow;
            await this.dbContext.SaveChangesAsync();

            await this.SeedAsync();

            Assert.Equal(36, this.dbContext.Services.Count());
            Assert.Equal(37, this.dbContext.Services.IgnoreQueryFilters().Count());
        }

        // Renaming a service changes its slug too (ServicesService.UpdateAsync), so the seeder
        // found neither and added the old service again beside the renamed one.
        [Fact]
        public async Task SeedAsyncShouldNotBringBackAServiceAnAdminRenamed()
        {
            await this.SeedAsync();

            Service renamed = this.dbContext.Services.Single(s => s.Slug == "tap-repairs");
            renamed.Name = "Tap & Mixer Repairs";
            renamed.Slug = "tap-mixer-repairs";
            await this.dbContext.SaveChangesAsync();

            await this.SeedAsync();

            Assert.Equal(37, this.dbContext.Services.Count());
            Assert.DoesNotContain(this.dbContext.Services, s => s.Name == "Tap Repairs");
        }

        // The seeder used to write its own price, duration and category over every existing row
        // at each start, so a change made in the admin panel lasted until the next deploy.
        [Fact]
        public async Task SeedAsyncShouldLeaveWhatAnAdminChangedAlone()
        {
            await this.SeedAsync();

            ServiceCategory handyman = this.dbContext.ServiceCategories.Single(c => c.Slug == "handyman");
            Service edited = this.dbContext.Services.Single(s => s.Slug == "tap-repairs");
            edited.BasePrice = 95.00m;
            edited.EstimatedDurationMinutes = 45;
            edited.CategoryId = handyman.Id;
            await this.dbContext.SaveChangesAsync();

            await this.SeedAsync();

            Service after = this.dbContext.Services.Single(s => s.Slug == "tap-repairs");
            Assert.Equal(95.00m, after.BasePrice);
            Assert.Equal(45, after.EstimatedDurationMinutes);
            Assert.Equal(handyman.Id, after.CategoryId);
        }

        // A service added in the admin panel with no picture still gets its image row, pointing at
        // the path its picture would have. This part of the seeder runs at every start.
        [Fact]
        public async Task SeedAsyncShouldGiveAnAdminsNewServiceItsImageRow()
        {
            await this.SeedAsync();

            ServiceCategory handyman = this.dbContext.ServiceCategories.Single(c => c.Slug == "handyman");
            var added = new Service
            {
                Name = "Fence Repairs",
                Slug = "fence-repairs",
                Description = "Replacing broken fence panels and posts.",
                BasePrice = 60.00m,
                EstimatedDurationMinutes = 60,
                CategoryId = handyman.Id,
            };
            this.dbContext.Services.Add(added);
            await this.dbContext.SaveChangesAsync();

            await this.SeedAsync();

            Assert.Equal(38, this.dbContext.Services.Count());
            Assert.Equal(
                "/images/services/fence-repairs-hero.webp",
                this.dbContext.ServiceImages.Single(i => i.ServiceId == added.Id).ImageUrl);
        }

        // As the application does it: each seeder, then a save (ApplicationDbContextSeeder).
        private async Task SeedAsync()
        {
            await new ServiceCategoriesSeeder().SeedAsync(this.dbContext, null);
            await this.dbContext.SaveChangesAsync();

            await new ServicesSeeder().SeedAsync(this.dbContext, null);
            await this.dbContext.SaveChangesAsync();
        }
    }
}
