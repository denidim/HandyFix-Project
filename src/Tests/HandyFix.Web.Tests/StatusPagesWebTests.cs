namespace HandyFix.Web.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;

    using HandyFix.Common;
    using HandyFix.Web.Services.Forms;

    using Microsoft.AspNetCore.Hosting;
    using Microsoft.Extensions.Configuration;

    using Xunit;

    // The site's own pages for a request that ends in an error status, and the limit on how
    // often one visitor may send a form (PROJECT_STATE.md Section 3cb).
    public class StatusPagesWebTests : IClassFixture<SqliteWebApplicationFactory>
    {
        private readonly SqliteWebApplicationFactory server;

        public StatusPagesWebTests(SqliteWebApplicationFactory server)
        {
            this.server = server;
        }

        // A page that is not there used to answer 404 with nothing in it, and the visitor saw the
        // browser's own error screen with no way back into the site.
        [Theory]
        [InlineData("/no-such-page")]
        [InlineData("/Services/plumbing/no-such-service")]
        [InlineData("/Areas/no-such-area")]
        public async Task APageThatIsNotThereIsAnsweredWithTheSitesOwnNotFoundPage(string url)
        {
            var response = await this.Browser().GetAsync(url);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
            Assert.Contains("We can't find that page.", content);
            Assert.Contains("<title>Page Not Found - " + GlobalConstants.SystemName + "</title>", content);

            // The whole site is one click away: its header, a way home, the phone number.
            Assert.Contains("Back to the home page", content);
            Assert.Contains("tel:" + GlobalConstants.BusinessPhoneInternational, content);
            Assert.Contains("href=\"/Contact\"", content);
        }

        // Only a browser asking for a page gets the page. A script fetching data, a crawler
        // probing for files and a payment provider calling back get the bare status as before,
        // and cost the site no page render.
        [Fact]
        public async Task ARequestThatDidNotAskForAPageGetsTheBareStatus()
        {
            var response = await this.server.CreateClient().GetAsync("/no-such-page");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Empty(await response.Content.ReadAsStringAsync());
        }

        // A form left open until its token expired answers 400. The request arrives at the status
        // page as it was sent, a POST with the same bad token, and must not be turned away again
        // before it can be told what happened.
        [Fact]
        public async Task AFormWhoseTokenExpiredIsToldSoOnTheSitesOwnPage()
        {
            var form = new Dictionary<string, string> { ["Name"] = "Jane Doe", ["__RequestVerificationToken"] = "expired" };

            var response = await this.Browser().PostAsync("/Contact", new FormUrlEncodedContent(form));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
            Assert.Contains("That didn't go through.", content);
            Assert.Contains("refresh the page and send it again", content);
        }

        [Theory]
        [InlineData(500, HttpStatusCode.InternalServerError, "Something went wrong on our side.")]
        [InlineData(503, HttpStatusCode.ServiceUnavailable, "Something went wrong on our side.")]
        [InlineData(403, HttpStatusCode.Forbidden, "This page isn't open to you.")]
        [InlineData(429, HttpStatusCode.TooManyRequests, "That's a lot of tries in a short while.")]

        // Opened by its own address with something that is not an error status, it is a page
        // that is not there, and never a "200 OK" under an error message.
        [InlineData(200, HttpStatusCode.NotFound, "We can't find that page.")]
        [InlineData(9999, HttpStatusCode.NotFound, "We can't find that page.")]
        public async Task TheStatusPageAnswersWithTheStatusItDescribes(int code, HttpStatusCode expected, string heading)
        {
            var response = await this.server.CreateClient().GetAsync("/StatusPage/" + code);

            Assert.Equal(expected, response.StatusCode);
            Assert.Contains(heading, WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
        }

        // One visitor may send the three forms so many times in ten minutes, between them. This
        // host allows two.
        [Fact]
        public async Task SendingFormsTooOftenIsAnsweredWithTheSitesOwnTooManyAttemptsPage()
        {
            HttpClient client = this.BrowserOnAHostAllowing(formPosts: 2, slotLookups: 100);
            Dictionary<string, string> enquiry = Enquiry("Counting how many times a form may be sent.");

            var first = await FormsWebTests.PostFormAsync(client, "/Contact", enquiry);
            var second = await FormsWebTests.PostFormAsync(client, "/JoinOurTeam", new Dictionary<string, string> { ["Name"] = "Jane Doe" });
            var third = await FormsWebTests.PostFormAsync(client, "/Contact", enquiry);

            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
            Assert.Equal(HttpStatusCode.OK, second.StatusCode);

            Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);
            var content = WebUtility.HtmlDecode(await third.Content.ReadAsStringAsync());
            Assert.Contains("That's a lot of tries in a short while.", content);
            Assert.Contains("tel:" + GlobalConstants.BusinessPhoneInternational, content);

            // How long to wait, for anything that reads it.
            var retryAfter = int.Parse(third.Headers.GetValues("Retry-After").Single());
            Assert.InRange(retryAfter, 1, 600);

            // Reading pages is not limited, only sending forms.
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Contact")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Booking")).StatusCode);
        }

        // The booking page asks for a day's slots each time a day is clicked. Its script reads
        // the bare status; it is not sent a page.
        [Fact]
        public async Task AskingForSlotsTooOftenIsAnsweredWithABare429()
        {
            HttpClient client = this.server
                .WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((context, configuration) => configuration.AddInMemoryCollection(
                    new Dictionary<string, string> { [RateLimits.SlotLookupsKey] = "2" })))
                .CreateClient();

            var first = await client.GetAsync("/Booking/GetSlots?date=2026-10-20");
            var second = await client.GetAsync("/Booking/GetSlots?date=2026-10-21");
            var third = await client.GetAsync("/Booking/GetSlots?date=2026-10-22");

            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
            Assert.Equal(HttpStatusCode.OK, second.StatusCode);
            Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);
            Assert.Empty(await third.Content.ReadAsStringAsync());
        }

        private static Dictionary<string, string> Enquiry(string message) => new Dictionary<string, string>
        {
            ["Name"] = "Jane Doe",
            ["Email"] = "jane.doe@example.com",
            ["PhoneNumber"] = "07700 900123",
            ["Category"] = "Plumbing",
            ["Message"] = message,
        };

        // A client that asks for pages the way a browser does.
        private HttpClient Browser()
        {
            HttpClient client = this.server.CreateClient();
            client.DefaultRequestHeaders.Accept.ParseAdd("text/html");
            return client;
        }

        private HttpClient BrowserOnAHostAllowing(int formPosts, int slotLookups)
        {
            HttpClient client = this.server
                .WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((context, configuration) => configuration.AddInMemoryCollection(
                    new Dictionary<string, string>
                    {
                        [RateLimits.FormPostsKey] = formPosts.ToString(),
                        [RateLimits.SlotLookupsKey] = slotLookups.ToString(),
                    })))
                .CreateClient();
            client.DefaultRequestHeaders.Accept.ParseAdd("text/html");
            return client;
        }
    }
}
