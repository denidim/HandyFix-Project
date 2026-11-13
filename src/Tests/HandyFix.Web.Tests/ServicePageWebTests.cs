namespace HandyFix.Web.Tests
{
    using System.Linq;
    using System.Net;
    using System.Text.Json;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    using HandyFix.Data;
    using HandyFix.Data.Models;

    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.DependencyInjection;

    using Xunit;

    // A service page's own blocks, written in the admin (PROJECT_STATE.md Section 3ep). Until
    // then every page of a division carried the same sentence about "mobile crews", the same two
    // boxes and the same three questions.
    public class ServicePageWebTests : IClassFixture<SqliteWebApplicationFactory>
    {
        private const string PipePage = "/Services/plumbing/pipe-repairs";

        // Every service that comes with the site has its own text since batch 8 (Section 3ey); one
        // the admin adds has none until it is written, so the tests add one.
        private const string NewPage = "/Services/small-building-works/loft-boarding";

        private readonly SqliteWebApplicationFactory server;

        public ServicePageWebTests(SqliteWebApplicationFactory server)
        {
            this.server = server;
        }

        [Fact]
        public async Task ThePageShowsTheBlocksItsServiceHas()
        {
            await this.WritePipeRepairsAsync();

            string html = WebUtility.HtmlDecode(await this.GetAsync(PipePage));

            Assert.Contains("What we usually find", html);
            Assert.Contains("<span class=\"material-symbols-outlined\" aria-hidden=\"true\">water_drop</span>", html);
            Assert.Contains("<strong>A weeping joint.</strong> Water beads on a fitting.", html);
            Assert.Contains("The visit, step by step", html);
            Assert.Matches("<ol class=\"service-steps\">\\s*<li><div>We turn the water off.</div></li>\\s*<li><div>We cut out the bad length.</div></li>", html);
            Assert.Contains("Before we arrive", html);
            Assert.Contains("What is included", html);
            Assert.Contains("<li>New fittings</li>", html);

            // "What is not" was left empty, so it is not drawn at all.
            Assert.DoesNotContain("What is not", html);

            Assert.Contains("Questions about pipe repairs", html);
            Assert.Contains("Can you fix a pipe under the floor?", html);
        }

        [Fact]
        public async Task ItsQuestionsAreTheOnesInItsRecordForSearchEngines()
        {
            await this.WritePipeRepairsAsync();

            string html = await this.GetAsync(PipePage);

            var faq = Regex.Matches(html, "<script type=\"application/ld(?:\\+|&#x2B;)json\">(.*?)</script>", RegexOptions.Singleline)
                .Select(m => JsonDocument.Parse(m.Groups[1].Value).RootElement)
                .Single(block => block.GetProperty("@type").GetString() == "FAQPage");
            var question = Assert.Single(faq.GetProperty("mainEntity").EnumerateArray());
            Assert.Equal("Can you fix a pipe under the floor?", question.GetProperty("name").GetString());
            Assert.Equal("Yes, where a board lifts & the pipe can be reached.", question.GetProperty("acceptedAnswer").GetProperty("text").GetString());
        }

        // A page with none of its own blocks yet, as a service the admin has just added: the
        // opening and the price line, no empty headings, none of the old fixed text, and no
        // questions for search engines.
        [Fact]
        public async Task APageWithoutItsOwnBlocksShowsOnlyItsOpening()
        {
            await this.AddServiceWithoutTextAsync();

            string html = WebUtility.HtmlDecode(await this.GetAsync(NewPage));

            Assert.Contains("Service Overview", html);
            Assert.Contains("Priced after a free survey at your home", html);
            Assert.DoesNotContain("What we usually find", html);
            Assert.DoesNotContain("The visit, step by step", html);
            Assert.DoesNotContain("Before we arrive", html);
            Assert.DoesNotContain("<h2 class=\"details-section-title\">Questions about", html);
            Assert.DoesNotContain("FAQPage", html);
            Assert.DoesNotContain("mobile crews", html);
            Assert.DoesNotContain("Do you cover my location?", html);
            Assert.DoesNotContain("Certified", html);
            Assert.Contains("Deposits, cancelling and paying are on our <a href=\"/FAQ\">FAQ page</a>.", html);
        }

        // Tap Repairs has its choice of other services, which takes the place of the three cards
        // of its division at the foot of the page.
        [Fact]
        public async Task TheSameVisitServicesReplaceTheCardsOfTheDivision()
        {
            string html = WebUtility.HtmlDecode(await this.GetAsync("/Services/plumbing/tap-repairs"));

            Assert.Contains("Worth doing in the same visit", html);
            Assert.Contains("<a href=\"/Services/plumbing/toilet-repairs\">", html);
            Assert.Contains("<a href=\"/Services/handyman/silicone-mastic-resealing\">", html);
            Assert.DoesNotContain("You Might Also Need", html);

            await this.AddServiceWithoutTextAsync();
            string other = WebUtility.HtmlDecode(await this.GetAsync(NewPage));
            Assert.DoesNotContain("Worth doing in the same visit", other);
            Assert.Contains("You Might Also Need", other);
        }

        [Fact]
        public async Task EveryTownPageIsListedByName()
        {
            string html = await this.GetAsync("/Services/handyman/gutter-clearing");

            var section = Regex.Match(html, "<div class=\"service-towns\">(.*?)</div>", RegexOptions.Singleline).Groups[1].Value;
            var towns = Regex.Matches(section, "<a href=\"/Areas/([a-z0-9-]+)\">").Select(m => m.Groups[1].Value).ToList();

            using IServiceScope scope = this.server.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Equal(db.ServiceAreas.Count(), towns.Count);
            Assert.Contains("chessington", towns);
            Assert.Contains("twickenham", towns);
        }

        private async Task WritePipeRepairsAsync()
        {
            using IServiceScope scope = this.server.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Service pipe = await db.Services.SingleAsync(s => s.Slug == "pipe-repairs");
            pipe.CommonFindings = "[water_drop] A weeping joint. Water beads on a fitting.\n[build] A split pipe.";
            pipe.VisitSteps = "We turn the water off.\nWe cut out the bad length.";
            pipe.BeforeWeArrive = "Find your stop tap.";
            pipe.Included = "New fittings";
            pipe.NotIncluded = null;

            // The page's own questions are replaced by the test's one: since batch 4 (Section 3eu)
            // the seeder gives Pipe Repairs questions of its own.
            db.ServiceFaqs.RemoveRange(db.ServiceFaqs.Where(f => f.ServiceId == pipe.Id));
            db.ServiceFaqs.Add(new ServiceFaq
            {
                ServiceId = pipe.Id,
                Question = "Can you fix a pipe under the floor?",
                Answer = "Yes, where a board lifts & the pipe can be reached.",
                DisplayOrder = 1,
            });

            await db.SaveChangesAsync();
        }

        // A small building service with only what the admin form asks for. The class shares one
        // database, so it is added once.
        private async Task AddServiceWithoutTextAsync()
        {
            using IServiceScope scope = this.server.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            if (await db.Services.AnyAsync(s => s.Slug == "loft-boarding"))
            {
                return;
            }

            ServiceCategory building = await db.ServiceCategories.SingleAsync(c => c.Slug == "small-building-works");
            db.Services.Add(new Service
            {
                Name = "Loft Boarding",
                Slug = "loft-boarding",
                Description = "Boarding a loft floor for storage.",
                BasePrice = 280.00m,
                EstimatedDurationMinutes = 480,
                CategoryId = building.Id,
            });
            await db.SaveChangesAsync();
        }

        private async Task<string> GetAsync(string url)
        {
            var response = await this.server.CreateClient().GetAsync(url);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }
    }
}
