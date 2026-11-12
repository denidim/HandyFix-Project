namespace HandyFix.Web.Tests
{
    using System.Linq;
    using System.Net;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    using HandyFix.Common;

    using Xunit;

    // Emergency plumbing is taken by phone and WhatsApp at any hour, at the emergency rate, and is
    // never booked online; every other service is booked Monday to Saturday, 9 to 5
    // (PROJECT_STATE Section 3eg).
    public class EmergencyServiceWebTests : IClassFixture<SqliteWebApplicationFactory>
    {
        private const string EmergencyPage = "/Services/plumbing/emergency-plumbing";

        private readonly SqliteWebApplicationFactory server;

        public EmergencyServiceWebTests(SqliteWebApplicationFactory server)
        {
            this.server = server;
        }

        [Fact]
        public async Task TheEmergencyPageOffersTheCallAndWhatsAppInPlaceOfTheBooking()
        {
            string html = WebUtility.HtmlDecode(await this.GetAsync(EmergencyPage));

            Assert.Contains("24/7 by Phone & WhatsApp", html);
            Assert.Contains("Plumbing Emergency? Call Us Now", html);
            Assert.Contains("Call " + GlobalConstants.BusinessPhone, html);
            Assert.Contains("href=\"tel:" + GlobalConstants.BusinessPhoneInternational + "\"", html);
            Assert.Contains("Message us on WhatsApp", html);
            Assert.Contains("£150 per Hour", html);
            Assert.Contains("60 to 90 Minutes", html);
            Assert.Contains("Pick a time that suits you online", html);

            Assert.DoesNotContain("Book a Slot", html);
            Assert.DoesNotContain("Request Your Booking", html);
            Assert.DoesNotContain("Book This Service Now", html);

            // The phone's floating button calls instead of opening the booking.
            string floating = Regex.Match(html, "<div class=\"mobile-cta-bar\">[\\s\\S]*?</div>").Value;
            Assert.Contains("tel:" + GlobalConstants.BusinessPhoneInternational, floating);
            Assert.Contains("Call Now", floating);
        }

        [Fact]
        public async Task TheEmergencyIsNotInTheBookingMenu()
        {
            string booking = WebUtility.HtmlDecode(await this.GetAsync("/Booking?categorySlug=plumbing"));

            Assert.DoesNotContain("Emergency Plumbing (from", booking);
            Assert.Contains("Tap Repairs (from", booking);
        }

        // "24/7" belongs to the one service taken at any hour. Every other plumbing page is
        // booked online and says "Pick a Time That Suits You" instead.
        [Fact]
        public async Task NoOtherPlumbingPageSays247()
        {
            string category = await this.GetAsync("/Services/plumbing");
            var pages = Regex.Matches(category, "href=\"(/Services/plumbing/[a-z0-9-]+)\"")
                .Select(m => m.Groups[1].Value)
                .Distinct()
                .Where(p => p != EmergencyPage)
                .ToList();

            Assert.NotEmpty(pages);
            foreach (string page in pages)
            {
                string html = await this.GetAsync(page);
                Assert.DoesNotContain("24/7", html);
            }
        }

        [Fact]
        public async Task TheCategoryPagesSayWhenEachIsAvailable()
        {
            string plumbing = WebUtility.HtmlDecode(await this.GetAsync("/Services/plumbing"));
            string handyman = WebUtility.HtmlDecode(await this.GetAsync("/Services/handyman"));

            Assert.Contains("Emergencies 24/7 by Phone", plumbing);
            Assert.Contains("24/7 by Phone</a>", plumbing);
            Assert.Contains("Mon–Sat, 9am–5pm", handyman);
            Assert.DoesNotContain("24/7", handyman);
        }

        [Fact]
        public async Task TheTermsAndTheContactPageGiveTheEmergencyHours()
        {
            string terms = WebUtility.HtmlDecode(await this.GetAsync("/TermsAndConditions"));
            string contact = WebUtility.HtmlDecode(await this.GetAsync("/Contact"));

            Assert.Contains("Emergency plumbing, taken by phone and WhatsApp at any hour, is charged at £150 per hour", terms);
            Assert.DoesNotContain("standard plumbing rate", terms);
            Assert.Contains("Bookings: Mon to Sat, 9am to 5pm", contact);
            Assert.Contains("Emergencies: 24 hours, by phone and WhatsApp", contact);
        }

        private async Task<string> GetAsync(string url)
        {
            var response = await this.server.CreateClient().GetAsync(url);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }
    }
}
