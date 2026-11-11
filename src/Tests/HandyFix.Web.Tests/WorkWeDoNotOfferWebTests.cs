namespace HandyFix.Web.Tests
{
    using System.Threading.Tasks;

    using Xunit;

    // The site promised work the company does not do: leak detection with a thermal camera,
    // "drainage" (we clear blockages inside the home only), and HVAC and electrical failures in
    // the FAQ. A promise we do not keep costs a review (PROJECT_STATE Sections 3dz and 3ea).
    public class WorkWeDoNotOfferWebTests : IClassFixture<SqliteWebApplicationFactory>
    {
        private readonly SqliteWebApplicationFactory server;

        public WorkWeDoNotOfferWebTests(SqliteWebApplicationFactory server)
        {
            this.server = server;
        }

        [Theory]
        [InlineData("/")]
        [InlineData("/Services")]
        [InlineData("/Services/plumbing")]
        [InlineData("/Services/plumbing/tap-repairs")]
        [InlineData("/Services/plumbing/leak-repairs")]
        [InlineData("/About")]
        [InlineData("/FAQ")]
        public async Task NoPagePromisesLeakDetectionOrDrainage(string url)
        {
            var response = await this.server.CreateClient().GetAsync(url);
            response.EnsureSuccessStatusCode();
            string html = await response.Content.ReadAsStringAsync();

            Assert.DoesNotContain("Leak Detection", html);
            Assert.DoesNotContain("thermal", html, System.StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Drainage", html);
            Assert.DoesNotContain("behind walls", html);
            Assert.DoesNotContain("HVAC", html);
            Assert.DoesNotContain("Electrical, etc.", html);
        }

        // There is no electrical category and no "dashboard"; booking is picking a job and a time.
        [Fact]
        public async Task TheFaqDescribesTheRealBookingAndEmergencies()
        {
            string faq = await this.server.CreateClient().GetStringAsync("/FAQ");

            Assert.Contains("Press Book Now, pick the job you need and a time that suits you", faq);
            Assert.Contains("We answer the phone 24/7, so call us about a burst pipe", faq);
            Assert.Contains("within 60 to 90 minutes", faq);
            Assert.DoesNotContain("dashboard", faq);
        }

        [Fact]
        public async Task ThePlumbingCardsNameWorkWeDo()
        {
            string home = await this.server.CreateClient().GetStringAsync("/");
            string services = await this.server.CreateClient().GetStringAsync("/Services");

            foreach (string html in new[] { home, services })
            {
                Assert.Contains("Leak Repairs", html);
                Assert.Contains("Blocked Sinks &amp; Toilets", html);
                Assert.Contains("Tap Repairs", html);
            }
        }
    }
}
