namespace HandyFix.Web.Tests
{
    using System.Linq;
    using System.Net;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    using Xunit;

    // What a page tells Google about itself, and the pages that answer "what does it cost" and
    // "who are you" (the SEO master plan's item 7). The texts are the ones read and accepted on
    // 14 November 2026. The rates and the deposit in them are read from the site, so the numbers
    // below are the seeded prices: 90 for plumbing, 60 for handyman, 150 for an emergency.
    public class PageTextsWebTests : IClassFixture<SqliteWebApplicationFactory>
    {
        private readonly SqliteWebApplicationFactory server;

        public PageTextsWebTests(SqliteWebApplicationFactory server)
        {
            this.server = server;
        }

        // A page that can be booked ends its title with "Book Online", the emergency page with the
        // call, a title that says it all with nothing, and a page that names no ending keeps the
        // firm's name. A description of null is the service's own summary, checked in
        // ServicesControllerTests.
        [Theory]
        [InlineData("/", "Plumbing Handyman Surrey: Book Online from £60 an Hour", "Local plumbers and handymen across Surrey and South London. Plumbing £90 an hour, handyman £60, no call-out fee. Pick a time that suits you and book online.")]
        [InlineData("/Services", "Plumbing & Handyman Services in Surrey - Book Online", "Every job we do in one list: plumbing at £90 an hour, handyman work at £60, and small building work at a fixed quote after a free survey. Book a slot online.")]
        [InlineData("/Pricing", "Plumber £90, Handyman £60 an Hour in Surrey - Book Online", "Plumbing is £90 an hour and handyman work £60, with a one-hour minimum and no call-out fee. Small building work is quoted after a free survey. Book online.")]
        [InlineData("/Booking", "Book a Plumber or Handyman Online in Surrey", "Pick the job, a day and a free time, Monday to Saturday, 9am to 5pm. A £50 deposit paid online secures your slot and comes off the final bill.")]
        [InlineData("/Contact", "Contact Us: Call, WhatsApp or Get a Quote in Surrey", "Call or WhatsApp 020 3951 5915 at any hour, or send us the details of a bigger job for a fixed quote after a free survey. We are based in Chessington.")]
        [InlineData("/FAQ", "FAQ: Prices, Booking and the £50 Deposit - Book Online", "How booking works, what an hour costs, how the £50 deposit and cancelling work, and what to do in an emergency. Plain answers before you book.")]
        [InlineData("/Areas", "Areas We Cover in Surrey & South London - Book Online", "We work within 15 miles of Chessington, across Surrey and South London. Find your town to see the postcodes we cover there, then book a slot online.")]
        [InlineData("/Reviews", "Customer Reviews of Our Plumbers & Handymen - Book Online", "Read what customers across Surrey and South London say about our plumbing and handyman work, and leave a review of your own on Google.")]
        [InlineData("/About", "About Us: Chessington Plumbers & Handymen - Book Online", "A plumbing and handyman firm based in Chessington, working within 15 miles across Surrey and South London. Meet the owner and see what you can check about us.")]
        [InlineData("/Services/plumbing", "Plumbing Services in Surrey from £90 an Hour - Book Online", "Taps, toilets, leaks, radiators and showers repaired and fitted across Surrey and South London, at £90 an hour with no call-out fee. Book a slot online.")]
        [InlineData("/Services/handyman", "Handyman Services in Surrey from £60 an Hour - Book Online", "Shelves, TVs, flat-pack furniture, doors, locks and small repairs across Surrey and South London, at £60 an hour with no call-out fee. Book a slot online.")]
        [InlineData("/Services/small-building-works", "Small Building & Refurbishments in Surrey, Free Survey", "Bathrooms, kitchens, tiling, plastering, flooring and partition walls across Surrey and South London, at a fixed quote after a free survey of the job.")]
        [InlineData("/Services/plumbing/tap-repairs", "Tap Repairs in Surrey from £90 - Book Online", null)]
        [InlineData("/Services/plumbing/emergency-plumbing", "Emergency Plumbing in Surrey from £150 - Call or WhatsApp", null)]
        [InlineData("/Services/small-building-works/kitchen-fitting-alterations", "Kitchen Fitting & Alterations in Surrey, Free Survey", null)]
        [InlineData("/Areas/chessington", "Plumber & Handyman in Chessington - Book Online", "Plumber and handyman in Chessington (KT9). Plumbing £90 an hour, handyman £60, no call-out fee. Pick a time that suits you and book online.")]
        [InlineData("/Areas/surbiton", "Plumber & Handyman in Surbiton - Book Online", "Plumber and handyman in Surbiton (KT5, KT6, KT7). Plumbing £90 an hour, handyman £60, no call-out fee. Pick a time that suits you and book online.")]
        [InlineData("/PrivacyPolicy", "Privacy Policy - Plumbing Handyman Surrey", null)]
        public async Task APageTellsGoogleItsOwnTitleAndDescription(string url, string title, string description)
        {
            string html = await this.GetAsync(url);

            // The test host is Development, so the title starts with the copy's mark.
            Assert.Equal("[DEVELOPMENT] " + title, Decoded(html, "<title>(.*?)</title>"));

            // The share card a chat app shows carries the same title, without the copy's mark.
            Assert.Equal(title, Decoded(html, "<meta property=\"og:title\" content=\"([^\"]*)\""));

            if (description != null)
            {
                Assert.Equal(description, Decoded(html, "<meta name=\"description\" content=\"([^\"]*)\""));
                Assert.Equal(description, Decoded(html, "<meta property=\"og:description\" content=\"([^\"]*)\""));
            }
        }

        // The Areas page and the About page send a visitor to a town's page "to see the postcodes
        // we cover there", and the town's description names them. The page did not show them.
        [Fact]
        public async Task ATownsPageShowsItsPostcodeDistricts()
        {
            string html = await this.GetAsync("/Areas/surbiton");

            Assert.Matches("location_on</span>\\s*Covering KT5, KT6, KT7\\s*</span>", html);
        }

        // Five price questions at most, none of them asked on the FAQ page too, with the rates
        // read from the admin's prices.
        [Fact]
        public async Task ThePricingPageAnswersThePriceQuestions()
        {
            string html = WebUtility.HtmlDecode(await this.GetAsync("/Pricing"));

            string[] questions = Regex.Matches(html, "<span class=\"font-headline-sm text-primary\">([^<]+)</span>")
                .Select(m => m.Groups[1].Value)
                .ToArray();
            Assert.Equal(
                new[]
                {
                    "How much does a plumber charge per hour?",
                    "How much does a handyman charge per hour?",
                    "Is there a minimum charge?",
                    "Do you charge a call-out fee or for travel?",
                    "Can I book several jobs in one visit?",
                },
                questions);

            Assert.Matches("£90 an hour, with a one-hour minimum\\. After the first hour, time is billed in 30-minute blocks", html);
            Assert.Matches("£60 an hour, on the same terms", html);
            Assert.Contains("<span>No VAT is added to our prices</span>", html);
            Assert.Contains("<span>Payment is due at the end of each visit by card, bank transfer or cash</span>", html);
        }

        // The FAQ sends the price questions to the Pricing page and says how the bill is paid.
        [Fact]
        public async Task TheFaqLeavesThePricesToThePricingPageAndSaysHowToPay()
        {
            string html = WebUtility.HtmlDecode(await this.GetAsync("/FAQ"));

            Assert.Contains("The rates are on the <a href=\"/Pricing\">Pricing page</a>.", html);
            Assert.DoesNotContain("jobs start with a one-hour minimum at the hourly rate for the service", html);
            Assert.Contains("How and when do I pay?", html);
            Assert.Contains("A £50 deposit is paid online when you book, and it comes off the final bill.", html);
            Assert.Contains("by bank transfer or in cash", html);
        }

        // "Who we are and what you can check", in the words the site already uses: the company
        // line, Gas Safe and insurance with no number and no sum, the rates from the admin's
        // prices, and every town's page linked from the database.
        [Fact]
        public async Task TheAboutPageSaysWhoWeAreAndWhatCanBeChecked()
        {
            string html = WebUtility.HtmlDecode(await this.GetAsync("/About"));

            Assert.Contains("About <span>Plumbing Handyman Surrey</span></h1>", html);
            Assert.Contains("Plumbing Handyman Surrey is a trading name of ZAP80 LTD, company number 12658426, registered in England and Wales.", html);
            Assert.Contains("All boiler work is carried out by a Gas Safe registered engineer.", html);
            Assert.Contains("We carry public liability insurance.", html);
            Assert.Contains("Plumbing is £90 an hour and handyman work £60, with a one-hour minimum, no call-out fee and no VAT on top.", html);
            Assert.Contains("<div class=\"hub-badge mb-2\">£90 an hour</div>", html);
            Assert.Contains("<div class=\"hub-badge mb-2\">£60 an hour</div>", html);

            // A real job photo with what it shows as its alt text.
            Assert.Matches("<img src=\"/images/about-job\\.webp[^\"]*\" alt=\"A technician in a high-visibility jacket checks a metal wall frame with a spirit level\\.\"", html);

            // Every town with a page, the new ones too.
            foreach (string slug in new[] { "chessington", "surbiton", "twickenham", "cheam" })
            {
                Assert.Contains($"<a href=\"/Areas/{slug}\">", html);
            }

            // What the old page said and nobody had confirmed, or said in American.
            foreach (string gone in new[] { "Who We", "protect your floors", "Rapid turnarounds", "prompt dispatch", "drywall", "itemized", "Turnkey", "Bespoke", "/hr" })
            {
                Assert.DoesNotContain(gone, html);
            }
        }

        // One card for the owner, the same picture and the same words on the About page and on
        // a service page. Only the heading differs: there his role follows the kind of work.
        [Theory]
        [InlineData("/About", "Hi, I'm Zapryan, the owner")]
        [InlineData("/Services/plumbing/tap-repairs", "Hi, I'm Zapryan – Owner & Lead Plumber in Surrey")]
        [InlineData("/Services/handyman/tv-mounting", "Hi, I'm Zapryan – Owner & Lead Handyman in Surrey")]
        [InlineData("/Services/small-building-works/kitchen-fitting-alterations", "Hi, I'm Zapryan – Projects Director in Surrey")]
        public async Task TheOwnersCardIsTheSameOnAboutAndOnAServicePage(string url, string heading)
        {
            string html = WebUtility.HtmlDecode(await this.GetAsync(url));

            Assert.Contains($"<h3 class=\"local-expert-name\">{heading}</h3>", html);
            Assert.Contains("I've been in the construction industry since 2004", html);
            Assert.Contains("my personal guarantee that the work is finished to perfection.", html);
            Assert.Matches("<img class=\"local-expert-avatar\" alt=\"Zapryan, the owner\" src=\"/images/expert-zap\\.webp", html);
        }

        private static string Decoded(string html, string pattern)
        {
            return WebUtility.HtmlDecode(Regex.Match(html, pattern, RegexOptions.Singleline).Groups[1].Value);
        }

        private async Task<string> GetAsync(string url)
        {
            var response = await this.server.CreateClient().GetAsync(url);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }
    }
}
