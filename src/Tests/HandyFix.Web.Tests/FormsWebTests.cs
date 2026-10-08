namespace HandyFix.Web.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Text.Json;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    using Xunit;

    // The three public forms through the whole stack: what the page gives the browser, and what
    // comes back when a form is sent (PROJECT_STATE.md Section 3cb).
    public class FormsWebTests : IClassFixture<SqliteWebApplicationFactory>
    {
        private readonly SqliteWebApplicationFactory server;

        public FormsWebTests(SqliteWebApplicationFactory server)
        {
            this.server = server;
        }

        // The stricter rules are RegularExpressionAttribute subclasses so that ASP.NET Core writes
        // them into the page and the browser checks them while the visitor types. If a rule ever
        // stops being one, it still works on the server and silently stops working here.
        [Theory]
        [InlineData("/Contact", "Name")]
        [InlineData("/Contact", "Email")]
        [InlineData("/Contact", "PhoneNumber")]
        [InlineData("/JoinOurTeam", "Name")]
        [InlineData("/JoinOurTeam", "Email")]
        [InlineData("/JoinOurTeam", "PhoneNumber")]
        [InlineData("/Booking", "CustomerFirstName")]
        [InlineData("/Booking", "CustomerLastName")]
        [InlineData("/Booking", "Email")]
        [InlineData("/Booking", "PhoneNumber")]
        [InlineData("/Booking", "Postcode")]
        public async Task PublicFormsGiveTheBrowserTheStricterRules(string url, string field)
        {
            var client = this.server.CreateClient();
            var content = await (await client.GetAsync(url)).Content.ReadAsStringAsync();

            var input = Regex.Match(content, "<input[^>]*\\sname=\"" + field + "\"[^>]*>").Value;
            Assert.NotEmpty(input);
            Assert.Contains("data-val-regex-pattern=\"", input);
            Assert.Contains("data-val-regex=\"", input);
        }

        [Fact]
        public async Task BookingPageAsksForThePostcodeAndKnowsTheDistrictsWeCover()
        {
            var client = this.server.CreateClient();
            var content = await (await client.GetAsync("/Booking")).Content.ReadAsStringAsync();

            Assert.Contains("id=\"Postcode\"", content);

            // The districts the page checks a typed postcode against come from the service areas.
            var written = Regex.Match(content, "const servedDistricts = (\\[[^\\r\\n]*\\]);");
            Assert.True(written.Success);
            var districts = JsonSerializer.Deserialize<string[]>(written.Groups[1].Value);
            Assert.Contains("KT9", districts);
            Assert.Contains("GU1", districts);
            Assert.Equal(districts.Distinct().OrderBy(d => d), districts);

            // Nothing has been turned down yet, so the "we don't cover this postcode" notice is hidden.
            Assert.Matches("id=\"postcode-not-served\"[^>]*\\shidden", content);
        }

        // On a phone the floating Book Now button sat on top of the booking form's own fields,
        // and linked to the page the visitor was on.
        [Fact]
        public async Task TheFloatingBookNowButtonIsLeftOffTheBookingPage()
        {
            var client = this.server.CreateClient();

            Assert.DoesNotContain("mobile-cta-bar", await (await client.GetAsync("/Booking")).Content.ReadAsStringAsync());
            Assert.Contains("mobile-cta-bar", await (await client.GetAsync("/")).Content.ReadAsStringAsync());
            Assert.Contains("mobile-cta-bar", await (await client.GetAsync("/Contact")).Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task BookingForAPostcodeWeDoNotCoverIsTurnedDownWithTheWayForward()
        {
            var client = this.server.CreateClient();
            Dictionary<string, string> fields = ValidBooking();
            fields["Postcode"] = "m1 1ae";

            var response = await PostFormAsync(client, "/Booking", fields);

            // The form again, not the payment page.
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
            Assert.Contains("we don't take online bookings for M1 yet", content);
            Assert.DoesNotMatch("id=\"postcode-not-served\"[^>]*\\shidden", content);
            Assert.Contains("id=\"postcode-enquiry-link\"", content);

            // What was typed is still there.
            Assert.Matches("name=\"CustomerFirstName\"[^>]*value=\"Ada\"", content);
            Assert.Matches("name=\"Postcode\"[^>]*value=\"m1 1ae\"", content);
        }

        [Fact]
        public async Task BookingWithDetailsTheRulesRefuseComesBackWithEachMessage()
        {
            var client = this.server.CreateClient();
            Dictionary<string, string> fields = ValidBooking();
            fields["CustomerFirstName"] = "Ada1";
            fields["Email"] = "ada@example";
            fields["PhoneNumber"] = "12345";
            fields["Postcode"] = "KT9";
            fields["ProblemDescription"] = "Tap drips.";

            var response = await PostFormAsync(client, "/Booking", fields);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
            Assert.Contains("Please use letters only.", content);
            Assert.Contains("Please enter a full email address", content);
            Assert.Contains("Please enter a UK phone number", content);
            Assert.Contains("Please enter a full UK postcode", content);
            Assert.Contains("at least 20 characters", content);
        }

        // A form's hidden fields (the antiforgery token among them) as a browser would send them
        // back, with the given fields on top.
        internal static async Task<HttpResponseMessage> PostFormAsync(HttpClient client, string url, Dictionary<string, string> fields)
        {
            var page = await (await client.GetAsync(url)).Content.ReadAsStringAsync();
            var form = new Dictionary<string, string>(HiddenFieldsOf(page));
            Assert.Contains("__RequestVerificationToken", form.Keys);

            foreach (KeyValuePair<string, string> field in fields)
            {
                form[field.Key] = field.Value;
            }

            return await client.PostAsync(url, new FormUrlEncodedContent(form));
        }

        internal static Dictionary<string, string> HiddenFieldsOf(string page)
        {
            var hidden = new Dictionary<string, string>();
            foreach (Match input in Regex.Matches(page, "<input[^>]*type=\"hidden\"[^>]*>"))
            {
                var name = Regex.Match(input.Value, "\\sname=\"([^\"]+)\"").Groups[1].Value;
                if (name.Length > 0)
                {
                    hidden[name] = WebUtility.HtmlDecode(Regex.Match(input.Value, "\\svalue=\"([^\"]*)\"").Groups[1].Value);
                }
            }

            return hidden;
        }

        private static Dictionary<string, string> ValidBooking() => new Dictionary<string, string>
        {
            ["CustomerFirstName"] = "Ada",
            ["CustomerLastName"] = "Lovelace",
            ["Email"] = "ada@example.com",
            ["PhoneNumber"] = "07700 900123",
            ["Address"] = "1 Analytical Engine Way, Chessington",
            ["Postcode"] = "KT9 1AA",
            ["ProblemDescription"] = "The kitchen tap has been dripping for a week.",
            ["SlotId"] = Guid.NewGuid().ToString(),
            ["ServiceId"] = Guid.NewGuid().ToString(),
        };
    }
}
