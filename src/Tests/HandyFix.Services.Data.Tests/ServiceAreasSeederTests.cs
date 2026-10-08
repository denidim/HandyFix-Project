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

    // Areas are seeded into an empty table and are the admin's from then on, like the services
    // (PROJECT_STATE.md Section 3cb). On Sqlite, so the foreign key between an area and its
    // questions is a real one.
    public sealed class ServiceAreasSeederTests : IDisposable
    {
        private readonly SqliteConnection connection;
        private readonly ApplicationDbContext dbContext;

        public ServiceAreasSeederTests()
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
        public async Task SeedAsyncShouldPutEveryAreaAndItsQuestionsIntoAnEmptyTable()
        {
            await this.SeedAsync();

            Assert.Equal(15, this.dbContext.ServiceAreas.Count());
            Assert.Equal(30, this.dbContext.ServiceAreaFaqs.Count());
            Assert.All(
                this.dbContext.ServiceAreas.Include(a => a.Faqs).ToList(),
                area => Assert.Equal(new[] { 1, 2 }, area.Faqs.Select(f => f.DisplayOrder).OrderBy(o => o)));
        }

        // Each area starts with the postcode districts it covers, which is what the booking form
        // checks a postcode against.
        [Fact]
        public async Task SeedAsyncShouldGiveEveryAreaItsPostcodeDistricts()
        {
            await this.SeedAsync();

            Assert.DoesNotContain(this.dbContext.ServiceAreas, a => string.IsNullOrWhiteSpace(a.PostcodeDistricts));
            Assert.Equal("KT9", this.dbContext.ServiceAreas.Single(a => a.Slug == "chessington").PostcodeDistricts);
            Assert.Equal("SW19, SW20, SM4", this.dbContext.ServiceAreas.Single(a => a.Slug == "wimbledon").PostcodeDistricts);
        }

        // The admin panel deletes an area for good. The seeder looked each area up by slug and
        // added the ones it did not find, so the area was back at the next start.
        [Fact]
        public async Task SeedAsyncShouldNotBringBackAnAreaAnAdminDeleted()
        {
            await this.SeedAsync();

            ServiceArea dorking = this.dbContext.ServiceAreas.Include(a => a.Faqs).Single(a => a.Slug == "dorking");
            this.dbContext.ServiceAreaFaqs.RemoveRange(dorking.Faqs);
            this.dbContext.ServiceAreas.Remove(dorking);
            await this.dbContext.SaveChangesAsync();

            await this.SeedAsync();

            Assert.Equal(14, this.dbContext.ServiceAreas.Count());
            Assert.DoesNotContain(this.dbContext.ServiceAreas, a => a.Slug == "dorking");
        }

        [Fact]
        public async Task SeedAsyncShouldNotGiveAnAreaItsSeededQuestionsBack()
        {
            await this.SeedAsync();

            ServiceArea cobham = this.dbContext.ServiceAreas.Include(a => a.Faqs).Single(a => a.Slug == "cobham");
            this.dbContext.ServiceAreaFaqs.RemoveRange(cobham.Faqs);
            await this.dbContext.SaveChangesAsync();

            await this.SeedAsync();

            Assert.Empty(this.dbContext.ServiceAreaFaqs.Where(f => f.ServiceAreaId == cobham.Id));
        }

        // As the application does it: the seeder, then a save (ApplicationDbContextSeeder).
        private async Task SeedAsync()
        {
            await new ServiceAreasSeeder().SeedAsync(this.dbContext, null);
            await this.dbContext.SaveChangesAsync();
        }
    }
}
