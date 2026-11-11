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

        // A quote-only service cannot be booked, so its floating button stays the plain one.
        [Theory]
        [InlineData("/Services/plumbing/boiler-installation-servicing")]
        [InlineData("/")]
        public async Task ElsewhereTheFloatingBookNowOpensThePlainBookingPage(string url)
        {
            string html = await this.GetAsync(url);

            Assert.Contains("<a href=\"/Booking\" class=\"btn btn-success btn-mobile-cta\">", html);
        }

        private async Task<string> GetAsync(string url)
        {
            var response = await this.server.CreateClient().GetAsync(url);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }
    }
}
