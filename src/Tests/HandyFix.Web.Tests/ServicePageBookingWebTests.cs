namespace HandyFix.Web.Tests
{
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    using Xunit;

    // A service page leads to the booking first: "Book a Slot" is the one filled button, on top,
    // and the phone's floating "Book Now" opens the booking with that service already chosen.
    // "Request Your Booking" stays, second, for those who do not want to pay a deposit.
    public class ServicePageBookingWebTests : IClassFixture<SqliteWebApplicationFactory>
    {
        private const string TapRepairsPage = "/Services/plumbing/tap-repairs";

        private readonly SqliteWebApplicationFactory server;

        public ServicePageBookingWebTests(SqliteWebApplicationFactory server)
        {
            this.server = server;
        }

        [Fact]
        public async Task BookASlotIsTheFirstAndFilledButtonAndRequestTheOutlinedOne()
        {
            string html = await this.GetAsync(TapRepairsPage);

            int bookASlot = html.IndexOf("<span>Book a Slot</span>");
            int request = html.IndexOf("<span>Request Your Booking</span>");
            Assert.True(bookASlot > 0 && request > bookASlot, "Book a Slot should come before Request Your Booking.");

            Assert.Matches("class=\"sidebar-booking-btn sidebar-booking-btn-book\" href=\"[^\"]+\">\\s*<span class=\"material-symbols-outlined\">calendar_month</span>\\s*<span>Book a Slot</span>", html);
            Assert.Matches("class=\"sidebar-booking-btn sidebar-booking-btn-outline\" href=\"/Contact[^\"]*\">\\s*<span>Request Your Booking</span>", html);
        }

        [Fact]
        public async Task TheFloatingBookNowOnAServicePageOpensTheBookingWithThatService()
        {
            string html = await this.GetAsync(TapRepairsPage);

            string bookASlotHref = Regex.Match(html, "class=\"sidebar-booking-btn sidebar-booking-btn-book\" href=\"([^\"]+)\"").Groups[1].Value;
            string bookNowHref = Regex.Match(html, "<a href=\"([^\"]+)\" class=\"btn btn-success btn-mobile-cta\"").Groups[1].Value;

            Assert.StartsWith("/Booking?selectedServiceId=", bookASlotHref);
            Assert.Contains("categorySlug=plumbing", bookASlotHref);
            Assert.Equal(bookASlotHref, bookNowHref);
        }

        [Theory]
        [InlineData("/")]
        [InlineData("/Services/plumbing")]
        public async Task ElsewhereTheFloatingBookNowOpensThePlainBookingPage(string url)
        {
            string html = await this.GetAsync(url);

            Assert.Matches("<a href=\"/Booking\" class=\"btn btn-success btn-mobile-cta\">\\s*Book Now\\s*</a>", html);
        }

        // Quote-only work is quoted for, not booked: on its pages the floating button is the page's
        // own "Request Free Estimate", with the same address as the sidebar's (Section 3fl). It used
        // to read "Book Now" and open the plain booking page.
        [Theory]
        [InlineData("/Services/plumbing/boiler-installation-servicing", "class=\"sidebar-booking-btn\" href=\"([^\"]+)\"", "/Contact?service=")]
        [InlineData("/Services/small-building-works/kitchen-fitting-alterations", "class=\"sidebar-booking-btn\" href=\"([^\"]+)\"", "/Contact?service=")]
        [InlineData("/Services/small-building-works", "href=\"([^\"]+)\" class=\"btn btn-primary sidebar-cta-btn\"", "/Contact?categorySlug=small-building-works")]
        public async Task OnAQuoteOnlyPageTheFloatingButtonRequestsAFreeEstimate(string url, string sidebarPattern, string expectedStart)
        {
            string html = await this.GetAsync(url);

            string sidebarHref = Regex.Match(html, sidebarPattern).Groups[1].Value;
            Match floating = Regex.Match(html, "<a href=\"([^\"]+)\" class=\"btn btn-success btn-mobile-cta\">\\s*([^<]+?)\\s*</a>");

            Assert.StartsWith(expectedStart, sidebarHref);
            Assert.Equal(sidebarHref, floating.Groups[1].Value);
            Assert.Equal("Request Free Estimate", floating.Groups[2].Value);
        }

        private async Task<string> GetAsync(string url)
        {
            var response = await this.server.CreateClient().GetAsync(url);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }
    }
}
