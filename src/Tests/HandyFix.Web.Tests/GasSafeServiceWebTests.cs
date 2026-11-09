namespace HandyFix.Web.Tests
{
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    using HandyFix.Web.Presentation;

    using Xunit;

    // The boiler service is gas work: its page and its card show the Gas Safe logo, and no other
    // service does (PROJECT_STATE.md Section 3do).
    public class GasSafeServiceWebTests : IClassFixture<SqliteWebApplicationFactory>
    {
        private const string BoilerPage = "/Services/plumbing/boiler-installation-servicing";

        private readonly SqliteWebApplicationFactory server;

        public GasSafeServiceWebTests(SqliteWebApplicationFactory server)
        {
            this.server = server;
        }

        [Fact]
        public async Task TheBoilerPageShowsTheGasSafeLogoInsteadOf247()
        {
            string html = await this.GetAsync(BoilerPage);

            Assert.Contains("Boiler Installation &amp; Servicing Specialists", html);
            Assert.Contains("class=\"gas-safe-logo-pill\" src=\"" + GasSafeLogo.Url, html);
            Assert.Contains("class=\"gas-safe-logo-feature\" src=\"" + GasSafeLogo.Url, html);
            Assert.DoesNotContain("24/7 Available", html);

            // The two boxes that fit a boiler visit, not taps and leak detection.
            Assert.Contains("Survey &amp; Planning", html);
            Assert.Contains("Gas Safe Registered", html);
            Assert.DoesNotContain("Leak Detection", html);
        }

        // The description is written in paragraphs, and the page draws each one as its own.
        [Fact]
        public async Task TheBoilerPageDrawsItsDescriptionInParagraphs()
        {
            string html = await this.GetAsync(BoilerPage);

            Assert.Contains("<p>We fit new boilers and service existing ones.", html);
            Assert.Contains("<p>A new boiler starts with a survey.", html);
            Assert.Contains("<p>A yearly service keeps your boiler safe", html);
        }

        [Fact]
        public async Task AnotherPlumbingPageKeepsItsOwnBadgeAndNoLogo()
        {
            string html = await this.GetAsync("/Services/plumbing/tap-repairs");

            Assert.Contains("24/7 Available", html);
            Assert.Contains("Leak Detection", html);
            Assert.DoesNotContain(GasSafeLogo.Url, html);
        }

        [Fact]
        public async Task OnlyTheBoilerCardInThePlumbingListShowsTheLogo()
        {
            string html = await this.GetAsync("/Services/plumbing");

            Assert.Single(Regex.Matches(html, "class=\"service-card-gas-safe\""));
            Assert.Single(Regex.Matches(html, "service-horizontal-card-gas-safe"));
        }

        [Fact]
        public async Task TheBoilerServiceIsQuoteOnlyAndNotInTheHourlyBookingSelect()
        {
            string boilerPage = await this.GetAsync(BoilerPage);

            Assert.Contains("Free Survey &amp; Fixed Quote", boilerPage);
            Assert.Contains("Day Rate / Survey", boilerPage);
            Assert.Contains("Request Free Estimate", boilerPage);
            Assert.Contains("Gas Safe registered engineer", boilerPage);
            Assert.DoesNotContain("Book a Slot", boilerPage);
            Assert.DoesNotContain("Starting Price", boilerPage);

            string bookingPage = await this.GetAsync("/Booking?categorySlug=plumbing");
            Assert.DoesNotContain("Boiler Installation &amp; Servicing", bookingPage);
            Assert.Contains("General Plumbing Maintenance", bookingPage);
        }

        private async Task<string> GetAsync(string url)
        {
            var response = await this.server.CreateClient().GetAsync(url);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }
    }
}
