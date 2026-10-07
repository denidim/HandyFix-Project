namespace HandyFix.Web.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text.Json;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    using HandyFix.Common;

    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Mvc.Testing;
    using Microsoft.AspNetCore.Routing;
    using Microsoft.Extensions.DependencyInjection;

    using WebOptimizer;

    using Xunit;

    public class WebTests : IClassFixture<SqliteWebApplicationFactory>
    {
        private readonly SqliteWebApplicationFactory server;

        public WebTests(SqliteWebApplicationFactory server)
        {
            this.server = server;
        }

        // The public pages the site-wide copy checks below run against.
        public static TheoryData<string> PublicPages => new TheoryData<string>
        {
            "/",
            "/About",
            "/Contact",
            "/FAQ",
            "/Reviews",
            "/Pricing",
            "/Services",
            "/Services/plumbing",
            "/Services/plumbing/tap-repairs",
            "/Areas",
            "/Areas/cobham",
            "/Booking",
            "/PrivacyPolicy",
            "/TermsAndConditions",
            "/CookiePolicy",
            "/Identity/Account/Login",
        };

        [Fact]
        public async Task IndexPageShouldReturnStatusCode200WithTitle()
        {
            var client = this.server.CreateClient();
            var response = await client.GetAsync("/");
            response.EnsureSuccessStatusCode();
            var responseContent = await response.Content.ReadAsStringAsync();
            Assert.Contains("<title>", responseContent);
        }

        [Fact]
        public async Task AccountManagePageRequiresAuthorization()
        {
            var client = this.server.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            var response = await client.GetAsync("Identity/Account/Manage");
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }

        [Fact]
        public async Task AreasIndexPageShouldReturnStatusCode200WithFeaturedArea()
        {
            var client = this.server.CreateClient();
            var response = await client.GetAsync("/Areas");
            response.EnsureSuccessStatusCode();
            var responseContent = await response.Content.ReadAsStringAsync();
            Assert.Contains("Our Areas", responseContent);
            Assert.Contains("Guildford", responseContent);
        }

        [Fact]
        public async Task AreaDetailsPageShouldReturnStatusCode200ForKnownSlugWithSeoTags()
        {
            var client = this.server.CreateClient();
            var response = await client.GetAsync("/Areas/chessington");
            response.EnsureSuccessStatusCode();
            var responseContent = await response.Content.ReadAsStringAsync();
            Assert.Contains("Chessington", responseContent);
            Assert.Contains("rel=\"canonical\"", responseContent);
            Assert.Contains("og:title", responseContent);
            Assert.Contains("FAQPage", responseContent);
            Assert.Contains("BreadcrumbList", responseContent);
        }

        [Fact]
        public async Task AreaDetailsPageShouldReturn404ForUnknownSlug()
        {
            var client = this.server.CreateClient();
            var response = await client.GetAsync("/Areas/not-a-real-area");
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task OldServiceAreasRouteShouldRedirectPermanentlyToAreas()
        {
            var client = this.server.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            var response = await client.GetAsync("/ServiceAreas");
            Assert.Equal(HttpStatusCode.MovedPermanently, response.StatusCode);
            Assert.EndsWith("/Areas", response.Headers.Location.ToString());
        }

        [Fact]
        public async Task PricingPageShouldContainRealClickableAreaLinks()
        {
            var client = this.server.CreateClient();
            var response = await client.GetAsync("/Pricing");
            response.EnsureSuccessStatusCode();
            var responseContent = await response.Content.ReadAsStringAsync();
            Assert.Contains("href=\"/Areas/", responseContent);
        }

        [Fact]
        public async Task SitemapShouldIncludeAreasIndexAndAreaDetailUrls()
        {
            var client = this.server.CreateClient();
            var response = await client.GetAsync("/sitemap.xml");
            response.EnsureSuccessStatusCode();
            var responseContent = await response.Content.ReadAsStringAsync();
            Assert.Contains("/Areas</loc>", responseContent);
            Assert.Contains("/Areas/guildford</loc>", responseContent);
        }

        [Fact]
        public async Task AreaDetailsPageNearbyAreasShouldBeOrderedByDriveTimeNotFeaturedStatus()
        {
            // Cobham (~20 min) is nearest to Wimbledon (~20 min) and Walton-on-Thames &
            // Weybridge (~22 min) - neither is IsFeatured, which is the regression this
            // guards: the "Nearby Areas" block must come from GetNearestAsync, not the
            // component's default featured-first listing. Checking for the area-card
            // link itself (not just the word "Wimbledon", which the footer also lists
            // as plain text) is what actually distinguishes the two behaviours.
            var client = this.server.CreateClient();
            var response = await client.GetAsync("/Areas/cobham");
            response.EnsureSuccessStatusCode();
            var responseContent = await response.Content.ReadAsStringAsync();
            Assert.Contains("/Areas/wimbledon", responseContent);
        }

        [Fact]
        public async Task SiteImagesShouldTellBrowsersToRevalidate()
        {
            // Images are regenerated in place under the same file names (PROJECT_STATE Section 3bd).
            // Without a Cache-Control header, browsers kept showing the old pictures after a swap.
            var client = this.server.CreateClient();
            var response = await client.GetAsync("/images/hero.webp");
            response.EnsureSuccessStatusCode();
            Assert.True(response.Headers.CacheControl?.NoCache);
        }

        [Fact]
        public async Task CategoryPageShowsCoverageMapWithoutMadeUpTechnicianCount()
        {
            // The old map placeholder claimed "3 Active Technicians Nearby", a made-up figure
            // (PROJECT_STATE Section 3be); the real coverage diagram replaced it.
            var client = this.server.CreateClient();
            var response = await client.GetAsync("/Services/plumbing");
            response.EnsureSuccessStatusCode();
            var responseContent = await response.Content.ReadAsStringAsync();
            Assert.Contains("coverage-map", responseContent);
            Assert.DoesNotContain("Active Technicians", responseContent);
        }

        [Fact]
        public async Task RegistrationIsClosedButLoginStaysOpen()
        {
            // Public sign-up is closed (roadmap L3 item 7): the page 404s and the login page no
            // longer links to it. Login itself must keep working - it is how the admin signs in.
            var client = this.server.CreateClient();
            var registerResponse = await client.GetAsync("/Identity/Account/Register");
            Assert.Equal(HttpStatusCode.NotFound, registerResponse.StatusCode);

            var loginResponse = await client.GetAsync("/Identity/Account/Login");
            loginResponse.EnsureSuccessStatusCode();
            var loginContent = await loginResponse.Content.ReadAsStringAsync();
            Assert.DoesNotContain("Account/Register", loginContent);
        }

        [Theory]
        [MemberData(nameof(PublicPages))]
        public async Task PublicPagesUseNoLongDashes(string url)
        {
            // Visitor-facing text uses a hyphen or two sentences, never an em-dash (decided in
            // PROJECT_STATE Section 3bb, swept in Section 3bl). Covers the raw character, the named
            // entity, and the numeric form Razor produces when it encodes one from code.
            var client = this.server.CreateClient();
            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();

            Assert.DoesNotContain("—", content);
            Assert.DoesNotContain("&mdash;", content);
            Assert.DoesNotContain("&#x2014;", content);
        }

        [Theory]
        [MemberData(nameof(PublicPages))]
        public async Task PublicPagesShowTheNewBrandOnceInTheTitleAndNeverTheOldName(string url)
        {
            // Visible rename to "Plumbing Handyman Surrey" (roadmap L1 item 1). The layout appends
            // the brand to every title, so a page that also typed it into its own title showed it
            // twice. "HandyFix." stays allowed: it is the internal codename in asset paths.
            var client = this.server.CreateClient();
            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();

            Assert.DoesNotContain("Handy Fix", content);
            Assert.DoesNotContain("handyfix.co", content);
            Assert.DoesNotMatch("HandyFix(?!\\.)", content);

            var title = Regex.Match(content, "<title>(.*?)</title>", RegexOptions.Singleline).Groups[1].Value;
            Assert.EndsWith(" - Plumbing Handyman Surrey", title);
            Assert.Single(Regex.Matches(title, "Plumbing Handyman Surrey"));
        }

        [Theory]
        [MemberData(nameof(PublicPages))]
        public async Task PublicPagesLinkTheRealContactDetailsAndNeverThePlaceholderNumber(string url)
        {
            // The phone, WhatsApp link and email live once in GlobalConstants (PROJECT_STATE Section
            // 3bu). Before that a made-up number was typed by hand into 12 views in three spellings,
            // one of them an old-brand number the rename had missed. The header and footer are on
            // every page, so every page must carry the real links. "07123456789" by itself stays
            // allowed: the booking form shows it as an example of what a customer types.
            var client = this.server.CreateClient();
            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();

            // Razor writes the "+" of a tel: link as an entity, which browsers decode in an attribute.
            Assert.Contains("tel:" + GlobalConstants.BusinessPhoneInternational, WebUtility.HtmlDecode(content));
            Assert.Contains("href=\"" + GlobalConstants.BusinessWhatsAppUrl + "\"", content);
            Assert.Contains("mailto:" + GlobalConstants.BusinessEmail, content);

            Assert.DoesNotContain("tel:07123456789", content);
            Assert.DoesNotContain("07123 456", content);

            // The line is a VoIP landline, so written messages go to WhatsApp and never to SMS.
            Assert.DoesNotContain("sms:", content);

            // A script block is never HTML-decoded, so each JSON-LD number is read the way a search
            // engine reads it, as a JSON string. An HTML entity in it would fail here.
            foreach (Match phone in Regex.Matches(content, "\"telephone\":\\s*(\"[^\"]*\")"))
            {
                Assert.Equal(GlobalConstants.BusinessPhoneInternational, JsonSerializer.Deserialize<string>(phone.Groups[1].Value));
            }
        }

        [Theory]
        [MemberData(nameof(PublicPages))]
        public async Task PublicPagesNameTheCompanyBehindTheTradingName(string url)
        {
            // A UK limited company has to say on its website who it is: the footer of every page
            // names the company, its number and its registered office beside the trading name
            // (PROJECT_STATE Section 3bu).
            var client = this.server.CreateClient();
            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();

            Assert.Contains("trading name of " + GlobalConstants.CompanyLegalName, content);
            Assert.Contains("company number " + GlobalConstants.CompanyNumber, content);
            Assert.Contains("Registered office: " + GlobalConstants.CompanyAddress, content);

            // The structured data used to carry a made-up office and postcode. Any address in it is
            // now the registered office, read as JSON the way the phone number is.
            Assert.DoesNotContain("Dispatch Office", content);
            foreach (Match postcode in Regex.Matches(content, "\"postalCode\":\\s*(\"[^\"]*\")"))
            {
                Assert.Equal(GlobalConstants.CompanyAddressPostcode, JsonSerializer.Deserialize<string>(postcode.Groups[1].Value));
            }
        }

        [Fact]
        public async Task HomePageStructuredDataNamesThePhoneAndTheCompanyAndNoSocialProfiles()
        {
            // The home page JSON-LD used to list two social profiles the business never had
            // (PROJECT_STATE Section 3bu, roadmap L1 item 2). "sameAs" comes back only with real ones.
            var client = this.server.CreateClient();
            var response = await client.GetAsync("/");
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();

            var phone = Regex.Match(content, "\"telephone\":\\s*(\"[^\"]*\")");
            Assert.True(phone.Success);
            Assert.Equal(GlobalConstants.BusinessPhoneInternational, JsonSerializer.Deserialize<string>(phone.Groups[1].Value));
            Assert.Contains("\"legalName\": \"" + GlobalConstants.CompanyLegalName + "\"", content);
            Assert.Contains("\"foundingDate\": \"" + GlobalConstants.CompanyIncorporatedOn + "\"", content);
            Assert.Contains("\"streetAddress\": \"" + GlobalConstants.CompanyAddressStreet + "\"", content);
            Assert.Contains("\"postalCode\": \"" + GlobalConstants.CompanyAddressPostcode + "\"", content);
            Assert.DoesNotContain("\"sameAs\"", content);
        }

        [Theory]
        [MemberData(nameof(PublicPages))]
        public async Task PublicPagesStructuredDataIsJsonWithNoHtmlEntities(string url)
        {
            // Razor HTML-encodes whatever it writes, and a script block is never HTML-decoded. A
            // value written there as plain Razor reached search engines with the entity still in
            // it: "&amp;" in 14 service and area names, "&#xA3;" for the pound sign on every bookable
            // service page (PROJECT_STATE Section 3bv). Such values go through Json.Serialize now.
            var client = this.server.CreateClient();
            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();

            foreach (var block in JsonLdBlocks(content))
            {
                Assert.DoesNotMatch("&(amp|quot|lt|gt|#x?[0-9A-Fa-f]+);", block);
                JsonDocument.Parse(block).Dispose();
            }
        }

        [Theory]
        [InlineData("/Services/plumbing/radiator-trv-replacement", "Radiator & TRV Replacement")]
        [InlineData("/Services/plumbing/radiator-trv-replacement", "How much does Radiator & TRV Replacement cost?")]
        [InlineData("/Services/plumbing/radiator-trv-replacement", "starts from £")]
        [InlineData("/Areas/worcester-park-ewell", "Worcester Park & Ewell")]
        public async Task StructuredDataHoldsNamesAndPricesAsTheyAreTyped(string url, string expected)
        {
            // The same bug, read the way a search engine reads the page: each JSON-LD block parsed
            // as JSON, and its text values compared with what a person typed into the admin panel.
            var client = this.server.CreateClient();
            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();

            var values = new List<string>();
            foreach (var block in JsonLdBlocks(content))
            {
                using var json = JsonDocument.Parse(block);
                values.AddRange(JsonStrings(json.RootElement));
            }

            Assert.Contains(values, value => value.Contains(expected));
        }

        [Theory]
        [InlineData("2026-10-20", "2026-10-20")]
        [InlineData("2026-10-20T15:30:00", "2026-10-20")]
        [InlineData("abc", "")]
        [InlineData("2026-13-45", "")]
        [InlineData("\\", "")]
        [InlineData("';alert(1);//", "")]
        [InlineData("\";alert(1);//", "")]
        [InlineData("</script><script>alert(1)</script>", "")]
        public async Task BookingPageOpensOnTheDateInTheLinkOnlyWhenItIsADate(string date, string expected)
        {
            // The date in /Booking?date=... comes from whoever made the link. Written into the
            // page's script with HTML encoding, one backslash swallowed the closing quote and
            // stopped the whole booking script (PROJECT_STATE Section 3bv), and anything that was
            // not a date opened the calendar on "undefined NaN" (Section 3bw). The model binder
            // now reads it as a date: a real one reaches the script as yyyy-MM-dd, anything else
            // as an empty string, which the script takes to mean today.
            var client = this.server.CreateClient();
            var response = await client.GetAsync("/Booking?date=" + Uri.EscapeDataString(date));
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();

            var written = Regex.Match(content, "let initialDateStr = (\"[^\\r\\n]*\");\\r?\\n");
            Assert.True(written.Success);
            Assert.Equal(expected, JsonSerializer.Deserialize<string>(written.Groups[1].Value));
        }

        [Fact]
        public async Task HeaderAndFooterLogosUseSeparateSvgIdsAndTheTabIconIsLinked()
        {
            // The logo is inline SVG, rendered in both the header and the footer (PROJECT_STATE
            // Sections 3bi and 3bq). With one shared set of ids the footer copy would silently
            // reuse the header's gradients and masks, so each placement passes its own prefix.
            var client = this.server.CreateClient();
            var response = await client.GetAsync("/");
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();

            Assert.Contains("id=\"nav-shield-cut\"", content);
            Assert.Contains("id=\"footer-shield-cut\"", content);
            var logoIds = Regex.Matches(content, "id=\"((?:nav|footer)-[^\"]+)\"").Select(m => m.Groups[1].Value).ToList();
            Assert.Equal(logoIds.Count, logoIds.Distinct().Count());
            Assert.Contains("href=\"/favicon.svg\"", content);
        }

        [Theory]
        [InlineData("/Pricing")]
        [InlineData("/Areas/chessington")]
        public async Task ServiceCardBookingLinksNameTheServiceAndItsCategory(string page)
        {
            // These cards once linked to /Booking?serviceId=..., a name the booking page does not
            // read, so every card opened on the general plumbing service (PROJECT_STATE Section
            // 3br). A mistyped route value fails silently; only the rendered link can show it.
            var client = this.server.CreateClient();
            var response = await client.GetAsync(page);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();

            var bookingLinks = Regex.Matches(content, "href=\"(/Booking\\?[^\"]*)\"")
                .Select(m => WebUtility.HtmlDecode(m.Groups[1].Value))
                .ToList();

            Assert.NotEmpty(bookingLinks);
            Assert.All(bookingLinks, link => Assert.Matches("[?&]selectedServiceId=[0-9a-fA-F-]{36}(&|$)", link));
            Assert.All(bookingLinks, link => Assert.Matches("[?&]categorySlug=(plumbing|handyman)(&|$)", link));
        }

        [Fact]
        public async Task BookingPageOpensOnTheCategoryOfTheServiceItIsGiven()
        {
            var client = this.server.CreateClient();
            var pricing = await (await client.GetAsync("/Pricing")).Content.ReadAsStringAsync();
            var handymanServiceId = Regex.Match(
                WebUtility.HtmlDecode(pricing),
                "/Booking\\?selectedServiceId=([0-9a-fA-F-]{36})&categorySlug=handyman").Groups[1].Value;
            Assert.NotEmpty(handymanServiceId);

            // The service alone, no category: the page must still open on Handyman.
            var response = await client.GetAsync("/Booking?selectedServiceId=" + handymanServiceId);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();

            Assert.Matches("value=\"handyman\"\\s+checked", content);
            Assert.DoesNotMatch("value=\"plumbing\"\\s+checked", content);
        }

        [Fact]
        public async Task BookingPageOpensOnPlumbingForACategoryItHasNoTabFor()
        {
            // The page's script reads the ticked tab first. A link naming a category with no tab
            // ticked neither, and the calendar and the slots never appeared (PROJECT_STATE
            // Section 3bz).
            var client = this.server.CreateClient();
            var response = await client.GetAsync("/Booking?categorySlug=no-such-category");
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();

            Assert.Matches("value=\"plumbing\"\\s+checked", content);
            Assert.DoesNotMatch("value=\"handyman\"\\s+checked", content);
        }

        [Fact]
        public async Task BookingLinkForSmallBuildingGoesToTheContactFormOnThatCategory()
        {
            // Building work is quoted, not booked. The link value has to be the name the Contact
            // page reads: a mistyped one would redirect and select nothing.
            var client = this.server.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            var response = await client.GetAsync("/Booking?categorySlug=small-building-works");

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.EndsWith("/Contact?categorySlug=small-building-works", response.Headers.Location.ToString());
        }

        [Theory]
        [InlineData("plumbing")]
        [InlineData("handyman")]
        public async Task BookingPageOffersAGeneralServiceInEachCategoryItCanBook(string category)
        {
            // The booking page opens a category on the service with "General" in its name.
            // Handyman had none, so it opened on the first one in the alphabet, a bath screen
            // fitting (PROJECT_STATE Section 3bz).
            var client = this.server.CreateClient();
            var response = await client.GetAsync("/Booking?categorySlug=" + category);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();

            Assert.Matches("data-category=\"" + category + "\"[^>]*>\\s*General ", content);
        }

        [Fact]
        public async Task HomeBookingWidgetOffersSmallBuildingAsAQuoteNotABooking()
        {
            // Small Building cannot be booked online. As an ordinary option in this list it led
            // to an empty booking page (PROJECT_STATE Sections 3bo and 3bz). The page script reads
            // data-quote-only and swaps the date and the booking button for the quote note.
            var client = this.server.CreateClient();
            var response = await client.GetAsync("/");
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();

            Assert.Contains("<option value=\"small-building-works\" data-quote-only=\"true\">Small Building Work</option>", content);
            Assert.Contains("data-quote-href=\"/Contact?categorySlug=small-building-works\"", content);
            Assert.Contains("To book small building work, get in touch", content);
            Assert.Equal(3, Regex.Matches(content, "\\sdata-book-step[\\s>]").Count);
        }

        [Fact]
        public async Task ContactPageOpensOnTheCategoryNamedInTheLink()
        {
            // The widget's quote link first sent ?category=..., the name of the form's own field.
            // The list then looked for an option whose value was the slug and selected nothing,
            // though the controller had set the right category (PROJECT_STATE Section 3bz). Only
            // the rendered page shows which option is selected.
            var client = this.server.CreateClient();
            var response = await client.GetAsync("/Contact?categorySlug=small-building-works");
            response.EnsureSuccessStatusCode();
            var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

            Assert.Matches("<option[^>]*selected[^>]*>Small Building & Refurbishments</option>", content);
        }

        [Fact]
        public void AdminServicesListIsReachedByNamingItsControllerAndArea()
        {
            // Two controllers are called ServicesController: the public one, fixed at /Services,
            // and the admin one. The admin one redirects to its list by these three values
            // (AdminServicesControllerTests); this is the address they must produce.
            var links = this.server.Services.GetRequiredService<LinkGenerator>();

            Assert.Equal("/Administration/Services", links.GetPathByAction("Index", "Services", new { area = "Administration" }));
            Assert.Equal("/Services", links.GetPathByAction("Index", "Services", new { area = string.Empty }));
        }

        [Fact]
        public void EveryStylesheetOnDiskIsInTheSiteBundle()
        {
            // The list in SiteStylesheets.cs is the only thing that loads a public stylesheet
            // (PROJECT_STATE Section 3bs). A file added under wwwroot/css and left off the list
            // would never reach a page, with nothing to say so.
            var webRoot = this.server.Services.GetRequiredService<IWebHostEnvironment>().WebRootPath;
            var onDisk = Directory
                .EnumerateFiles(Path.Combine(webRoot, "css"), "*.css", SearchOption.AllDirectories)
                .Select(path => "/" + Path.GetRelativePath(webRoot, path).Replace('\\', '/'))
                .Where(path => path != "/css/pages/admin.css") // the admin layout links this one itself
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToList();

            Assert.Equal(onDisk, SiteStylesheets.SourceFiles.OrderBy(path => path, StringComparer.Ordinal).ToList());
        }

        [Fact]
        public async Task DevelopmentPagesLinkEachStylesheetWithItsOwnVersionInCascadeOrder()
        {
            // A page two levels deep: the links must start at the site root, or they would point
            // at /Services/css/... and every nested page would lose its styles.
            var client = this.server.CreateClient();
            var response = await client.GetAsync("/Services/plumbing");
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();

            var linked = Regex.Matches(content, "href=\"(/css/[^\"?]+\\.css)\\?v=[\\w-]+\"")
                .Select(m => m.Groups[1].Value)
                .ToList();

            Assert.Equal(SiteStylesheets.SourceFiles, linked);
        }

        [Fact]
        public async Task LiveSitePagesLinkOneCombinedStylesheetThatBrowsersMayKeep()
        {
            // Staging and production combine the stylesheets and let browsers keep the result; the
            // test host is Development, so this switches both on the way Program.cs does there.
            using var live = this.server.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
                services.Configure<WebOptimizerOptions>(options =>
                {
                    options.EnableTagHelperBundling = true;
                    options.EnableCaching = true;
                })));
            var client = live.CreateClient();

            var pageResponse = await client.GetAsync("/Services/plumbing");
            pageResponse.EnsureSuccessStatusCode();
            var page = await pageResponse.Content.ReadAsStringAsync();

            // One link, and its address carries a hash of the combined content. That hash is what
            // changes when any source file changes, so no browser keeps a stale copy.
            var link = Assert.Single(Regex.Matches(page, "href=\"(/css/[^\"]+)\"")).Groups[1].Value;
            Assert.Matches("^/css/site\\.min\\.css\\?v=[\\w-]+$", link);

            var response = await client.GetAsync(link);
            response.EnsureSuccessStatusCode();
            Assert.Equal("text/css", response.Content.Headers.ContentType?.MediaType);
            Assert.True(response.Headers.CacheControl?.MaxAge >= TimeSpan.FromDays(365));

            var css = await response.Content.ReadAsStringAsync();
            Assert.StartsWith("@import url(", css); // web fonts, which must open the file
            Assert.Equal(2, Regex.Matches(css, "@import").Count); // and no stylesheet of ours is left as an import
            Assert.Contains(".site-logo", css); // a file from the middle of the list
            Assert.Contains(".material-symbols-filled", css); // the last file
        }

        // The JSON-LD blocks of a page. The "+" in the tag's type is rendered as an entity, which
        // browsers decode inside an attribute, so both spellings are matched.
        private static IEnumerable<string> JsonLdBlocks(string html)
        {
            return Regex.Matches(html, "<script type=\"application/ld(?:\\+|&#x2B;)json\">(.*?)</script>", RegexOptions.Singleline)
                .Select(match => match.Groups[1].Value);
        }

        // Every text value in a JSON document, at any depth, unescaped.
        private static IEnumerable<string> JsonStrings(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.String:
                    yield return element.GetString();
                    break;
                case JsonValueKind.Object:
                    foreach (var property in element.EnumerateObject())
                    {
                        foreach (var value in JsonStrings(property.Value))
                        {
                            yield return value;
                        }
                    }

                    break;
                case JsonValueKind.Array:
                    foreach (var item in element.EnumerateArray())
                    {
                        foreach (var value in JsonStrings(item))
                        {
                            yield return value;
                        }
                    }

                    break;
            }
        }
    }
}
