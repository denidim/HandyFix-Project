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

    using HandyFix.Common;
    using HandyFix.Data;
    using HandyFix.Services.Messaging;
    using HandyFix.Web.Services.Forms;

    using Microsoft.AspNetCore.Hosting;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;

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

        // Through the whole stack, with a real slot from the test database: the page that comes
        // back tells its script the slot's day and the slot, so the calendar opens there and the
        // slot is picked again.
        [Fact]
        public async Task ABookingFormThatComesBackOpensOnTheDayAndSlotTheCustomerHadChosen()
        {
            Guid slotId;
            DateTime slotDay;
            using (IServiceScope scope = this.server.Services.CreateScope())
            {
                ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var slot = dbContext.AvailabilitySlots.Where(s => !s.IsBooked && !s.IsBlocked).OrderByDescending(s => s.StartTime).First();
                slotId = slot.Id;
                slotDay = slot.StartTime.Date;
            }

            var client = this.server.CreateClient();
            Dictionary<string, string> fields = ValidBooking();
            fields["SlotId"] = slotId.ToString();
            fields["Postcode"] = "m1 1ae";

            var response = await PostFormAsync(client, "/Booking", fields);

            var content = await response.Content.ReadAsStringAsync();
            Assert.Contains("let initialDateStr = \"" + slotDay.ToString("yyyy-MM-dd") + "\";", content);
            Assert.Contains("let keptSlotId = \"" + slotId + "\";", content);
        }

        // A page opened fresh has no slot to go back to.
        [Fact]
        public async Task ABookingPageOpenedFreshHasNoSlotToPickAgain()
        {
            var content = await (await this.server.CreateClient().GetAsync("/Booking")).Content.ReadAsStringAsync();

            Assert.Contains("let keptSlotId = \"\";", content);
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

        // A double click on Send used to save an enquiry twice. The same email and words again
        // are thanked and saved once; different words are a new enquiry.
        [Fact]
        public async Task TheSameEnquirySentTwiceIsThankedTwiceAndSavedOnce()
        {
            var client = this.server.CreateClient();
            Dictionary<string, string> enquiry = ValidEnquiry("The outside tap drips whenever it rains hard.");

            var first = await PostFormAsync(client, "/Contact", enquiry);
            var second = await PostFormAsync(client, "/Contact", enquiry);

            Assert.Contains("Request Received!", await first.Content.ReadAsStringAsync());
            Assert.Contains("Request Received!", await second.Content.ReadAsStringAsync());
            Assert.Equal(1, this.CountEnquiries("The outside tap drips whenever it rains hard."));

            var different = await PostFormAsync(client, "/Contact", ValidEnquiry("The outside tap drips whenever it rains hard. Also the hose."));

            Assert.Contains("Request Received!", await different.Content.ReadAsStringAsync());
            Assert.Equal(2, this.CountEnquiries("The outside tap drips whenever it rains hard."));
        }

        [Fact]
        public async Task TheSameApplicationSentTwiceIsSavedOnce()
        {
            var client = this.server.CreateClient();
            var application = new Dictionary<string, string>
            {
                ["Name"] = "Sam Fitter",
                ["Email"] = "sam.fitter@example.com",
                ["PhoneNumber"] = "07700 900456",
                ["Trade"] = "Plumbing",
                ["YearsExperience"] = "12",
                ["Availability"] = "Full-time",
                ["HasOwnTools"] = "true",
                ["HasOwnTransport"] = "true",
                ["AboutYou"] = "Twelve years fitting bathrooms around Epsom and Ewell.",
            };

            var first = await PostFormAsync(client, "/JoinOurTeam", application);
            var second = await PostFormAsync(client, "/JoinOurTeam", application);

            Assert.Contains("Application Received!", await first.Content.ReadAsStringAsync());
            Assert.Contains("Application Received!", await second.Content.ReadAsStringAsync());
            Assert.Equal(1, this.CountEnquiries("Twelve years fitting bathrooms around Epsom and Ewell."));
        }

        // Through the real controller and service, with only the email sender swapped for one
        // that keeps what it is given: an enquiry reaches the company's inbox and the sender
        // hears it arrived.
        [Fact]
        public async Task AnEnquiryIsAnnouncedToTheCompanyAndAcknowledgedToTheSender()
        {
            var emails = new RecordingEmailSender();
            var client = this.server
                .WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddSingleton<IEmailSender>(emails)))
                .CreateClient();

            var response = await PostFormAsync(client, "/Contact", ValidEnquiry("The stopcock under the sink will not turn at all."));

            Assert.Contains("Request Received!", await response.Content.ReadAsStringAsync());
            Assert.Equal(2, emails.Sent.Count);

            RecordingEmailSender.Email notice = emails.Sent.Single(e => e.To == GlobalConstants.BusinessEmail);
            Assert.Equal("New enquiry - Jane Doe", notice.Subject);
            Assert.Equal("jane.doe@example.com", notice.ReplyTo);
            Assert.Contains("[Category: Plumbing] The stopcock under the sink will not turn at all.", notice.Body);

            RecordingEmailSender.Email acknowledgement = emails.Sent.Single(e => e.To == "jane.doe@example.com");
            Assert.Equal("We have received your enquiry", acknowledgement.Subject);
            Assert.DoesNotContain("stopcock", acknowledgement.Body);
        }

        // Each public form carries the form guard's two fields: a text box moved off the page that
        // no person fills in, and a stamp of when the form was shown.
        [Theory]
        [InlineData("/Contact")]
        [InlineData("/JoinOurTeam")]
        [InlineData("/Booking")]
        public async Task PublicFormsCarryTheHiddenBoxAndTheStamp(string url)
        {
            var client = this.server.CreateClient();
            var content = await (await client.GetAsync(url)).Content.ReadAsStringAsync();

            Assert.Matches("<div class=\"form-extra\" aria-hidden=\"true\">\\s*<label[^>]*>[^<]*</label>\\s*<input type=\"text\"[^>]*name=\"Reference\"[^>]*tabindex=\"-1\"", content);
            Assert.Matches("<input type=\"hidden\" name=\"FormStamp\" value=\"[^\"]{20,}\"", content);
        }

        // Sent the instant it was fetched, as a program does. This host has the real three-second
        // minimum (the shared one has none, or no test here could send a form). The reply is the
        // same thank-you a person gets; nothing is saved and no email goes out.
        [Fact]
        public async Task AnEnquirySentFasterThanAPersonTypesIsThankedAndDropped()
        {
            var emails = new RecordingEmailSender();
            var client = this.server
                .WithWebHostBuilder(builder =>
                {
                    builder.ConfigureAppConfiguration((context, configuration) => configuration.AddInMemoryCollection(
                        new Dictionary<string, string> { [FormGuard.MinimumSecondsKey] = "3" }));
                    builder.ConfigureServices(services => services.AddSingleton<IEmailSender>(emails));
                })
                .CreateClient();

            var response = await PostFormAsync(client, "/Contact", ValidEnquiry("Sent by a program the moment the page loaded."));

            Assert.Contains("Request Received!", await response.Content.ReadAsStringAsync());
            Assert.Equal(0, this.CountEnquiries("Sent by a program the moment the page loaded."));
            Assert.Empty(emails.Sent);
        }

        [Theory]
        [InlineData("/Contact", "Request Received!")]
        [InlineData("/JoinOurTeam", "Application Received!")]
        public async Task ASubmissionWithTheHiddenBoxFilledInIsThankedAndDropped(string url, string thanks)
        {
            var client = this.server.CreateClient();
            Dictionary<string, string> fields = url == "/Contact"
                ? ValidEnquiry("Every field filled in, the hidden one too.")
                : new Dictionary<string, string>
                {
                    ["Name"] = "Robo Filler",
                    ["Email"] = "robo@example.com",
                    ["PhoneNumber"] = "07700 900789",
                    ["Trade"] = "Plumbing",
                    ["YearsExperience"] = "3",
                    ["Availability"] = "Full-time",
                    ["AboutYou"] = "Every field filled in, the hidden one too.",
                };
            fields[FormGuard.HoneypotFieldName] = "https://spam.example";

            var response = await PostFormAsync(client, url, fields);

            Assert.Contains(thanks, await response.Content.ReadAsStringAsync());
            Assert.Equal(0, this.CountEnquiries("Every field filled in, the hidden one too."));
        }

        // A booking cannot be "thanked": its success is the payment page. A program gets the form
        // back with a message that gives nothing away and still tells a person how to book.
        [Fact]
        public async Task ABookingWithTheHiddenBoxFilledInComesBackWithAPhoneNumberAndIsNotTaken()
        {
            var client = this.server.CreateClient();
            Dictionary<string, string> fields = ValidBooking();
            fields[FormGuard.HoneypotFieldName] = "filled";

            var response = await PostFormAsync(client, "/Booking", fields);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
            Assert.Contains("We could not take this booking online. Please call us or message us on WhatsApp on " + GlobalConstants.BusinessPhone, content);
            Assert.Equal(0, this.CountBookings());
        }

        // A form that comes back with an error keeps the stamp it was sent with, so the person who
        // corrects it and sends again a second later is timed from when they first saw the form.
        [Fact]
        public async Task AFormThatComesBackWithAnErrorKeepsTheStampItWasSentWith()
        {
            var client = this.server.CreateClient();
            var page = await (await client.GetAsync("/Contact")).Content.ReadAsStringAsync();
            Dictionary<string, string> form = HiddenFieldsOf(page);
            var sentStamp = form[FormGuard.StampFieldName];
            foreach (KeyValuePair<string, string> field in ValidEnquiry("short"))
            {
                form[field.Key] = field.Value;
            }

            var response = await client.PostAsync("/Contact", new FormUrlEncodedContent(form));

            var content = await response.Content.ReadAsStringAsync();
            Assert.Contains("Message must be at least 10 characters long.", content);
            Assert.Equal(sentStamp, HiddenFieldsOf(content)[FormGuard.StampFieldName]);
        }

        // With keys, each public form shows Cloudflare's "are you a person" check just above its
        // button, told which form it is on, and loads Cloudflare's script for it.
        [Theory]
        [InlineData("/Contact", "contact")]
        [InlineData("/JoinOurTeam", "join-team")]
        [InlineData("/Booking", "booking")]
        public async Task WithKeysEachPublicFormShowsThePersonCheckForThatForm(string url, string action)
        {
            var client = this.server
                .WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddSingleton<ITurnstileVerifier>(new FakeTurnstile())))
                .CreateClient();

            var content = await (await client.GetAsync(url)).Content.ReadAsStringAsync();

            Assert.Contains("<div class=\"cf-turnstile\" data-sitekey=\"site-key-for-tests\" data-action=\"" + action + "\"", content);
            Assert.Contains("<script src=\"https://challenges.cloudflare.com/turnstile/v0/api.js\" async defer></script>", content);

            // Inside the form, and before its button.
            var form = content.Substring(content.IndexOf("<form", content.IndexOf("<main", StringComparison.Ordinal), StringComparison.Ordinal));
            form = form.Substring(0, form.IndexOf("</form>", StringComparison.Ordinal));
            Assert.InRange(form.IndexOf("cf-turnstile", StringComparison.Ordinal), 0, form.LastIndexOf("type=\"submit\"", StringComparison.Ordinal));
        }

        // Without keys the check is off, which is allowed on a developer's machine only: no
        // widget, no script from Cloudflare.
        [Theory]
        [InlineData("/Contact")]
        [InlineData("/JoinOurTeam")]
        [InlineData("/Booking")]
        public async Task WithoutKeysInDevelopmentNoPersonCheckIsShown(string url)
        {
            var content = await (await this.server.CreateClient().GetAsync(url)).Content.ReadAsStringAsync();

            Assert.DoesNotContain("cf-turnstile", content);
            Assert.DoesNotContain("challenges.cloudflare.com", content);
        }

        // The check did not pass. A person can land here, so the form comes back as typed with a
        // message and a way round it; nothing is saved and no email goes out. When it passes,
        // the enquiry is saved as usual.
        [Fact]
        public async Task AnEnquiryIsSavedOnlyWhenThePersonCheckPasses()
        {
            var turnstile = new FakeTurnstile { Passes = false };
            var emails = new RecordingEmailSender();
            var client = this.server
                .WithWebHostBuilder(builder => builder.ConfigureServices(services =>
                {
                    services.AddSingleton<ITurnstileVerifier>(turnstile);
                    services.AddSingleton<IEmailSender>(emails);
                }))
                .CreateClient();
            Dictionary<string, string> enquiry = ValidEnquiry("The person check stands between this and the inbox.");

            var refused = await PostFormAsync(client, "/Contact", enquiry);

            var content = WebUtility.HtmlDecode(await refused.Content.ReadAsStringAsync());
            Assert.Contains("We could not confirm that you are a person and not a program.", content);
            Assert.Contains("call us or message us on WhatsApp on " + GlobalConstants.BusinessPhone, content);
            Assert.Contains("The person check stands between this and the inbox.", content);
            Assert.DoesNotContain("Request Received!", content);
            Assert.Equal(0, this.CountEnquiries("The person check stands between this and the inbox."));
            Assert.Empty(emails.Sent);

            turnstile.Passes = true;
            var accepted = await PostFormAsync(client, "/Contact", enquiry);

            Assert.Contains("Request Received!", await accepted.Content.ReadAsStringAsync());
            Assert.Equal(1, this.CountEnquiries("The person check stands between this and the inbox."));
            Assert.Equal(new[] { "contact", "contact" }, turnstile.AskedAbout);
        }

        [Fact]
        public async Task ABookingIsNotTakenWhenThePersonCheckDoesNotPass()
        {
            var turnstile = new FakeTurnstile { Passes = false };
            var client = this.server
                .WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddSingleton<ITurnstileVerifier>(turnstile)))
                .CreateClient();

            var response = await PostFormAsync(client, "/Booking", ValidBooking());

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
            Assert.Contains("We could not confirm that you are a person and not a program.", content);
            Assert.Equal(new[] { "booking" }, turnstile.AskedAbout);
            Assert.Equal(0, this.CountBookings());
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

        private static Dictionary<string, string> ValidEnquiry(string message) => new Dictionary<string, string>
        {
            ["Name"] = "Jane Doe",
            ["Email"] = "jane.doe@example.com",
            ["PhoneNumber"] = "07700 900123",
            ["Category"] = "Plumbing",
            ["Message"] = message,
        };

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

        // Enquiries in the test host's own database whose message holds this text.
        private int CountEnquiries(string text)
        {
            using IServiceScope scope = this.server.Services.CreateScope();
            ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            return dbContext.Inquiries.Count(x => x.Message.Contains(text));
        }

        private int CountBookings()
        {
            using IServiceScope scope = this.server.Services.CreateScope();
            ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            return dbContext.Bookings.Count();
        }
    }
}
