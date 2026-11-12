namespace HandyFix.Web.Tests
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    using HandyFix.Data;

    using Microsoft.Extensions.DependencyInjection;

    using Xunit;

    // A town's page shows its own four services as short cards, each leading to the service's
    // own page (PROJECT_STATE.md Section 3el). Until then every town printed the same four in
    // full, so the boiler's whole description stood on every area page and no card led to a
    // service page.
    public class AreaServiceCardsWebTests : IClassFixture<SqliteWebApplicationFactory>
    {
        private const string CardName =
            "<h3 class=\"pricing-card-title\"><a class=\"pricing-card-title-link\" href=\"(/Services/(plumbing|handyman)/[a-z0-9-]+)\">";

        private readonly SqliteWebApplicationFactory server;

        public AreaServiceCardsWebTests(SqliteWebApplicationFactory server)
        {
            this.server = server;
        }

        [Fact]
        public async Task EachCardLeadsToItsServicesOwnPage()
        {
            string html = await this.GetAsync("/Areas/cobham");

            var names = Regex.Matches(html, CardName);
            Assert.Equal(new[] { "plumbing", "plumbing", "handyman", "handyman" }, names.Select(m => m.Groups[2].Value));

            // A page that opens, not only an address of the right shape.
            await this.GetAsync(names[0].Groups[1].Value);
        }

        [Fact]
        public async Task TwoTownsShowDifferentServices()
        {
            Assert.NotEqual(await this.CardLinksAsync("/Areas/cobham"), await this.CardLinksAsync("/Areas/chessington"));
        }

        [Fact]
        public async Task NoAreaPageCarriesAWholeDescription()
        {
            foreach (string slug in this.AreaSlugs())
            {
                string html = WebUtility.HtmlDecode(await this.GetAsync("/Areas/" + slug));

                var texts = Regex.Matches(html, "<p class=\"pricing-card-desc\">(.*?)</p>", RegexOptions.Singleline)
                    .Select(m => m.Groups[1].Value)
                    .ToList();
                Assert.Equal(4, texts.Count);
                Assert.All(texts, text => Assert.True(text.Length < 300, $"{slug}: a card carries {text.Length} characters"));

                // The boiler description's third sentence, on every area page until Section 3el.
                Assert.DoesNotContain("A new boiler starts with a survey", html);
            }
        }

        // The same card on the Pricing page keeps a plain name and its picture goes to the booking.
        [Fact]
        public async Task ThePricingPagesCardsKeepTheirPlainNames()
        {
            string html = await this.GetAsync("/Pricing");

            Assert.Contains("<h3 class=\"pricing-card-title\">", html);
            Assert.DoesNotContain("pricing-card-title-link", html);
            Assert.Matches("<a class=\"pricing-card-media card-image-link\" href=\"/Booking\\?", html);
        }

        private async Task<IReadOnlyList<string>> CardLinksAsync(string url)
        {
            return Regex.Matches(await this.GetAsync(url), CardName).Select(m => m.Groups[1].Value).ToList();
        }

        private IReadOnlyList<string> AreaSlugs()
        {
            using IServiceScope scope = this.server.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            return db.ServiceAreas.Select(x => x.Slug).ToList();
        }

        private async Task<string> GetAsync(string url)
        {
            var response = await this.server.CreateClient().GetAsync(url);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }
    }
}
