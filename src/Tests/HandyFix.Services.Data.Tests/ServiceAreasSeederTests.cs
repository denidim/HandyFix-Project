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

            // The first fifteen with two questions each; the six of Section 3em with one.
            Assert.Equal(21, this.dbContext.ServiceAreas.Count());
            Assert.Equal(36, this.dbContext.ServiceAreaFaqs.Count());
            Assert.All(
                this.dbContext.ServiceAreas.Include(a => a.Faqs).ToList(),
                area => Assert.Equal(Enumerable.Range(1, area.Faqs.Count), area.Faqs.Select(f => f.DisplayOrder).OrderBy(o => o)));
        }

        // The six towns added in November 2026 (PROJECT_STATE Section 3em) come with their
        // districts, their drive time and their text in paragraphs. The AddSixNewTownAreas
        // migration holds the same six for a database that already had areas.
        [Theory]
        [InlineData("twickenham", "Twickenham", "TW1, TW2", 25, "Montpelier Row")]
        [InlineData("richmond", "Richmond", "TW9, TW10", 29, "Maids of Honour Row")]
        [InlineData("molesey", "Molesey", "KT8", 13, "Howard Houses")]
        [InlineData("new-malden", "New Malden", "KT3", 9, "the six Groves")]
        [InlineData("ashtead", "Ashtead", "KT21", 14, "the spring line")]
        [InlineData("cheam", "Cheam", "SM3, SM2", 15, "mock Tudor shops")]
        public async Task SeedAsyncShouldAddTheSixNewTowns(string slug, string name, string districts, int minutes, string words)
        {
            await this.SeedAsync();

            ServiceArea area = this.dbContext.ServiceAreas.Include(a => a.Faqs).Single(a => a.Slug == slug);
            Assert.Equal(name, area.Name);
            Assert.Equal(districts, area.PostcodeDistricts);
            Assert.Equal(minutes, area.DriveTimeMinutes);
            Assert.False(area.IsFeatured);
            Assert.NotNull(area.DiagramBearing);
            Assert.Contains(words, area.IntroCopy);

            // A blank line between paragraphs, which the town's page draws as separate ones.
            Assert.Contains("\n\n", area.IntroCopy);
            Assert.Contains("\n\n", area.LocalNeighbourhoodsCopy);
            Assert.Single(area.Faqs);
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

        // The diagram places moved out of _AreaCoverageMap.cshtml into the areas themselves
        // (PROJECT_STATE Section 3dm): every town but the centre gets one, with its old name and side.
        [Fact]
        public async Task SeedAsyncShouldGiveEveryTownItsPlaceOnTheDiagram()
        {
            await this.SeedAsync();

            Assert.DoesNotContain(this.dbContext.ServiceAreas, a => a.Slug != "chessington" && a.DiagramBearing == null);
            Assert.Null(this.dbContext.ServiceAreas.Single(a => a.Slug == "chessington").DiagramBearing);

            var kingston = this.dbContext.ServiceAreas.Single(a => a.Slug == "kingston-upon-thames");
            Assert.Equal(10, kingston.DiagramBearing);
            Assert.Equal("Kingston", kingston.DiagramLabel);

            var dorking = this.dbContext.ServiceAreas.Single(a => a.Slug == "dorking");
            Assert.Equal(DiagramLabelSide.Below, dorking.DiagramLabelSide);
            Assert.Null(dorking.DiagramLabel);
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

            Assert.Equal(20, this.dbContext.ServiceAreas.Count());
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
