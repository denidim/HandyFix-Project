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

            Assert.Equal(38, this.dbContext.Services.Count());
            Assert.Equal(
                new[] { "general-handyman-call-out", "general-plumbing-maintenance" },
                this.dbContext.Services.Where(s => s.DisplayOrder == 0).Select(s => s.Slug).OrderBy(s => s));
            Assert.Equal(4, this.dbContext.Services.Count(s => s.IsPopular));
            Assert.Equal(38, this.dbContext.ServiceImages.Count());

            // Only the boiler service carries the Gas Safe logo, and it is a plumbing one that is quoted rather than booked by the hour.
            Service boiler = this.dbContext.Services.Include(s => s.Category).Single(s => s.IsGasSafeWork);
            Assert.Equal("boiler-installation-servicing", boiler.Slug);
            Assert.Equal("plumbing", boiler.Category.Slug);
            Assert.Equal(90.00m, boiler.BasePrice);
            Assert.Equal(60, boiler.EstimatedDurationMinutes);
            Assert.True(boiler.IsQuoteOnly);
            Assert.Equal(9, this.dbContext.Services.Count(s => s.IsQuoteOnly));

            // Emergency plumbing is the one service taken by phone at any hour, at the emergency rate
            // (PROJECT_STATE Section 3eg).
            Service emergency = this.dbContext.Services.Single(s => s.IsEmergency);
            Assert.Equal("emergency-plumbing", emergency.Slug);
            Assert.Equal(150.00m, emergency.BasePrice);
            Assert.False(emergency.IsQuoteOnly);
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

            Assert.Equal(37, this.dbContext.Services.Count());
            Assert.Equal(38, this.dbContext.Services.IgnoreQueryFilters().Count());
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

            Assert.Equal(38, this.dbContext.Services.Count());
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

            Assert.Equal(39, this.dbContext.Services.Count());
            Assert.Equal(
                "/images/services/fence-repairs-hero.webp",
                this.dbContext.ServiceImages.Single(i => i.ServiceId == added.Id).ImageUrl);
        }

        // A new database gets the written pages' own text, as an existing one got it from the
        // migration of its batch (AddServicePageTextsBatch1 to 4, PROJECT_STATE.md Sections 3eq, 3er,
        // 3es and 3eu): the description, the blocks a point per line, and the questions in order.
        [Theory]
        [InlineData("tap-repairs", "A tap that drips", "[water_drop] A worn washer.", 3, "Is it worth repairing a tap, or should I replace it?")]
        [InlineData("toilet-repairs", "Running, slow to fill", "[water_drop] A flush valve that no longer seals.", 2, "Why does my toilet keep running?")]
        [InlineData("leak-repairs", "A leak we can see or reach", "[build] A loose or worn compression joint.", 2, "How can I tell if there is a leak I cannot see?")]
        [InlineData("blocked-drains", "We clear blockages inside the house", "[water_drop] Fat in a kitchen waste.", 3, "Who clears a blocked drain outside my house?")]
        [InlineData("boiler-installation-servicing", "We fit new boilers and service existing ones.", "[warning] Low pressure.", 3, "How often should a boiler be serviced?")]
        [InlineData("radiator-trv-replacement", "A thermostatic radiator valve, or TRV,", "[refresh] A stuck TRV pin.", 3, "Can a TRV be changed without draining the heating?")]
        [InlineData("tv-mounting", "We fix a flat-screen TV to the wall", "[search] A plasterboard wall.", 2, "Can a TV go on a plasterboard wall?")]
        [InlineData("furniture-assembly", "Wardrobes, beds, chests of drawers", "[checklist] A part missing or damaged in the box.", 2, "What happens if a part is missing?")]
        [InlineData("shelf-installation", "The wall decides how a shelf is fixed", "[warning] Plasterboard stuck to brick.", 2, "How much weight can a floating shelf hold?")]
        [InlineData("full-bathroom-refurbishment", "A new bathroom from bare walls", "[water_drop] Soft floorboards under the old bath or toilet.", 2, "Does a bathroom refit need building regulations approval?")]
        [InlineData("emergency-plumbing", "This is the one job we take at any hour", "[warning] A pipe split by frost.", 3, "Who is responsible for a leak between the street and my house?")]
        [InlineData("shower-installation", "Which shower a home can take", "[construction] A valve built into the wall, or one on its face.", 2, "Can I have a power shower with a combi boiler?")]
        [InlineData("kitchen-plumbing", "The plumbing at the kitchen sink", "[build] A trap that drips.", 2, "Does the kitchen cold tap have to come from the mains?")]
        [InlineData("washing-machine-dishwasher-install", "A new washing machine, washer-dryer or dishwasher", "[warning] Transit bolts still in.", 2, "Does a washing machine need a hot water connection?")]
        [InlineData("outside-garden-tap-installation", "We fit a tap to an outside wall", "[warning] An old tap with no backflow protection.", 2, "Should I turn my outside tap off in winter?")]
        [InlineData("pipe-repairs", "Pipes split in a frost", "[warning] A split left by frost.", 2, "Who pays to replace a lead pipe?")]
        [InlineData("bathroom-plumbing", "A new basin, bath, bidet or toilet", "[construction] A wall-hung basin on a stud wall.", 2, "Do I need to tell the water company about a new bath?")]
        [InlineData("general-plumbing-maintenance", "An hour spent going round the plumbing", "[plumbing] A stop valve that will not turn, or will not shut.", 2, "What happens if the stop valve is seized?")]
        [InlineData("door-trimming-shaving", "New carpet, laminate or tiles raise the floor", "[home] A door that drags across its whole width.", 2, "How much can you take off a door?")]
        [InlineData("door-repairs", "When a door sticks in its frame", "[build] Loose hinge screws.", 2, "Why does my door stick only in winter?")]
        [InlineData("silicone-mastic-resealing", "When the sealant round a bath", "[water_drop] Black mould inside the bead.", 2, "How long before I can use the bath or shower?")]
        [InlineData("wall-mounting", "Pictures, heavy mirrors, whiteboards", "[warning] A mirror that is heavier than it looks.", 2, "How heavy a mirror can go on plasterboard?")]
        [InlineData("lock-handle-replacement", "We replace door locks, latches and handles", "[lock] A euro cylinder that can be snapped.", 2, "What lock does my home insurance need?")]
        [InlineData("bath-shower-screen-fitting", "You buy the glass bath screen or shower screen", "[water_drop] A screen sealed on the wrong side.", 2, "Which side of a bath screen should be sealed?")]
        [InlineData("minor-electrical-tasks", "This is the small electrical work a handyman visit covers", "[bolt] A metal light fitting and no earth wire at the ceiling.", 2, "Do I need an electrician to change a light fitting?")]
        public async Task SeedAsyncShouldGiveTheWrittenPagesTheirText(string slug, string descriptionStart, string firstFinding, int questions, string firstQuestion)
        {
            await this.SeedAsync();

            Service service = this.dbContext.Services.Include(s => s.Faqs).Single(s => s.Slug == slug);
            Assert.StartsWith(descriptionStart, service.Description);
            Assert.StartsWith(firstFinding, service.CommonFindings);
            Assert.False(string.IsNullOrWhiteSpace(service.Summary));
            Assert.True(service.Summary.Length <= 200);
            Assert.Contains("\n", service.VisitSteps);
            Assert.DoesNotContain("\r", service.VisitSteps);
            Assert.False(string.IsNullOrWhiteSpace(service.BeforeWeArrive));
            Assert.False(string.IsNullOrWhiteSpace(service.Included));
            Assert.Equal(questions, service.Faqs.Count);
            Assert.Equal(firstQuestion, service.Faqs.OrderBy(f => f.DisplayOrder).First().Question);

            // The words of things we do not do stay off the pages (master plan decision 10).
            Assert.DoesNotContain("thermal", service.Description + service.CommonFindings + service.NotIncluded, StringComparison.OrdinalIgnoreCase);

            // Nor American trade words (master plan R5: TV Mounting said "drywall", Kitchen Plumbing
            // "garbage disposals").
            Assert.DoesNotContain("drywall", service.Description + service.Summary, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("garbage", service.Description + service.Summary, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task SeedAsyncShouldLeaveTheOtherPagesBlocksEmpty()
        {
            await this.SeedAsync();

            Service gutters = this.dbContext.Services.Include(s => s.Faqs).Single(s => s.Slug == "gutter-clearing");
            Assert.Null(gutters.Summary);
            Assert.Null(gutters.CommonFindings);
            Assert.Empty(gutters.Faqs);
            Assert.Equal(25, this.dbContext.ServiceFaqs.Select(f => f.ServiceId).Distinct().Count());
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
