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

        // The Identity UI package's own "manage your account" pages used to answer here: a
        // redirect to the login page for a visitor, the stock pages for a signed-in admin. They
        // are closed now; AdminAccountWebTests has the whole list (PROJECT_STATE.md Section 3cc).
        [Fact]
        public async Task AccountManagePageIsNotThere()
        {
            var client = this.server.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            var response = await client.GetAsync("Identity/Account/Manage");
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
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

        // The icon font was asked for twice, once with every weight, 1.1 MB on every page; it was
        // asked for once at the one weight the site draws icons at (PROJECT_STATE Section 3cn), and
        // is now the site's own file holding only the icons it uses (Section 3cz).
        [Fact]
        public async Task PagesAskForTheIconFontOnceFromTheSiteItself()
        {
            var client = this.server.CreateClient();
            var page = await client.GetStringAsync("/");

            var iconFont = Assert.Single(Regex.Matches(page, "@font-face \\{[^}]*font-family: 'Material Symbols Outlined';[^}]*\\}")).Value;
            Assert.Matches("src: url\\(/fonts/material-symbols-outlined\\.woff2\\?v=[\\w-]+\\)", iconFont);
            Assert.Contains("font-display: block;", iconFont);
            Assert.Equal(2, Regex.Matches(page, "@font-face \\{[^}]*font-family: 'Outfit';").Count); // Latin and the extra Latin letters
        }

        // Every page the sitemap hands a search engine names itself as the page. Only the area
        // pages did (PROJECT_STATE Section 3cm); a page added to the sitemap without a canonical
        // link fails here.
        [Fact]
        public async Task EveryPageInTheSitemapNamesItselfAsTheCanonicalAddress()
        {
            var client = this.server.CreateClient();
            var sitemap = await client.GetStringAsync("/sitemap.xml");
            var addresses = Regex.Matches(sitemap, "<loc>([^<]+)</loc>")
                .Select(m => WebUtility.HtmlDecode(m.Groups[1].Value))
                .ToList();
            Assert.NotEmpty(addresses);

            foreach (var address in addresses)
            {
                var page = await client.GetStringAsync(address);
                var canonical = Regex.Match(page, "<link rel=\"canonical\" href=\"([^\"]+)\"");
                Assert.True(canonical.Success, $"{address} has no canonical link");
                Assert.Equal(address, WebUtility.HtmlDecode(canonical.Groups[1].Value));
            }
        }

        // A link with a query string, or in other letter case, shows the same page, and the page
        // names the one address the sitemap gives for it, so a search engine counts them as one.
        // Upper case in a slug is the same case on the live database, whose comparisons ignore
        // case; the test database's do not, so it is not asked here.
        [Theory]
        [InlineData("/?utm_source=facebook", "/")]
        [InlineData("/Booking?categorySlug=handyman&date=2026-10-20", "/Booking")]
        [InlineData("/Contact?service=Tap%20Repairs", "/Contact")]
        [InlineData("/services/plumbing", "/Services/plumbing")]
        [InlineData("/SERVICES/plumbing/tap-repairs", "/Services/plumbing/tap-repairs")]
        public async Task APageReachedThroughAnotherAddressNamesItsOwn(string link, string canonicalPath)
        {
            var client = this.server.CreateClient();
            var page = await client.GetStringAsync(link);
            var canonical = Regex.Match(page, "<link rel=\"canonical\" href=\"([^\"]+)\"");
            Assert.True(canonical.Success, $"{link} has no canonical link");
            Assert.Equal(new Uri(client.BaseAddress, canonicalPath).ToString(), WebUtility.HtmlDecode(canonical.Groups[1].Value));
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

        // A screen reader user moves through a page by its headings; a heading two levels below the
        // one before it reads as a section gone missing. Each of these skipped a level somewhere,
        // the footer's titles on every page (PROJECT_STATE Section 3co).
        [Theory]
        [InlineData("/")]
        [InlineData("/Booking")]
        [InlineData("/Services/plumbing")]
        public async Task HeadingsNeverSkipALevel(string url)
        {
            var client = this.server.CreateClient();
            var page = await client.GetStringAsync(url);
            var markup = Regex.Replace(page, "<script\\b.*?</script>", string.Empty, RegexOptions.Singleline);

            var previous = 0;
            foreach (Match heading in Regex.Matches(markup, "<h([1-6])\\b[^>]*>(.*?)</h\\1>", RegexOptions.Singleline))
            {
                var level = int.Parse(heading.Groups[1].Value);
                Assert.True(level <= previous + 1, $"h{level} \"{Regex.Replace(heading.Groups[2].Value, "<[^>]+>|\\s+", " ").Trim()}\" follows an h{previous}");
                previous = level;
            }
        }

        // The cookie banner's button was named "Close" for a screen reader while it reads "Accept",
        // and its link said "Learn More" without saying of what (PROJECT_STATE Section 3co).
        [Fact]
        public async Task CookieBannerSaysWhatItsButtonAndLinkDo()
        {
            var client = this.server.CreateClient();
            var page = await client.GetStringAsync("/");

            var banner = Regex.Match(page, "<div id=\"cookieConsent\".*?</div>", RegexOptions.Singleline).Value;
            Assert.Contains(">Read our Cookie Policy</a>", banner);
            Assert.DoesNotContain("aria-label", banner);
            Assert.Matches("<button[^>]*accept-policy[^>]*>\\s*Accept\\s*</button>", banner);
        }

        // A file linked with its version in the address may be kept for a year, since a changed file
        // gets a new address; Bootstrap and jQuery had no stated lifetime (PROJECT_STATE Section 3cn).
        [Fact]
        public async Task AVersionedFileIsKeptForAYear()
        {
            var client = this.server.CreateClient();
            var response = await client.GetAsync("/lib/bootstrap/dist/css/bootstrap.min.css?v=abc");
            response.EnsureSuccessStatusCode();
            Assert.Equal(TimeSpan.FromDays(365), response.Headers.CacheControl?.MaxAge);
            Assert.True(response.Headers.CacheControl?.Public);
        }

        // A category card shows its picture 88px wide on a phone; with the small copy offered the
        // browser takes it there and the whole picture on a computer (PROJECT_STATE Section 3cn).
        [Fact]
        public async Task CategoryCardsOfferThePicturesSmallCopy()
        {
            var client = this.server.CreateClient();
            var page = await client.GetStringAsync("/Services/plumbing");

            var card = Regex.Match(page, "<img class=\"service-card-thumbnail\" alt=\"Tap Repairs\"[^>]*>").Value;
            var picture = Regex.Match(card, "src=\"([^\"?]+)").Groups[1].Value;
            Assert.EndsWith("-hero.webp", picture);
            Assert.Contains($"srcset=\"{picture[..^".webp".Length]}-320.webp 320w, {picture} 1024w\"", card);
            Assert.Contains("sizes=\"(max-width: 767.98px) 88px, 256px\"", card);
        }

        // Every service picture that comes with the site has its small copy; a picture added or
        // replaced without running tools/image-gen/make-phone-copies.mjs fails here.
        [Fact]
        public void EveryServicePictureHasItsSmallCopy()
        {
            var webRoot = this.server.Services.GetRequiredService<IWebHostEnvironment>().WebRootPath;
            var folder = Path.Combine(webRoot, "images", "services");
            var pictures = Directory.GetFiles(folder, "*-hero.webp");
            Assert.NotEmpty(pictures);
            Assert.All(pictures, picture => Assert.True(File.Exists(picture[..^".webp".Length] + "-320.webp"), $"{Path.GetFileName(picture)} has no small copy"));
        }

        // Every service picture that comes with the site has its middle copy, which the service
        // cards on the home, area, pricing and service pages offer (PROJECT_STATE Section 3cz).
        [Fact]
        public void EveryServicePictureHasItsMiddleCopy()
        {
            var webRoot = this.server.Services.GetRequiredService<IWebHostEnvironment>().WebRootPath;
            var pictures = Directory.GetFiles(Path.Combine(webRoot, "images", "services"), "*-hero.webp");
            Assert.NotEmpty(pictures);
            Assert.All(pictures, picture => Assert.True(File.Exists(picture[..^".webp".Length] + "-640.webp"), $"{Path.GetFileName(picture)} has no middle copy"));
        }

        // The service cards offer the middle copy beside the picture, on every page that has them.
        [Theory]
        [InlineData("/", "<img src=\"/images/services/plumbing-category-hero\\.webp[^>]*>")]
        [InlineData("/Areas/cobham", "<img class=\"pricing-card-image\"[^>]*>")]
        [InlineData("/Pricing", "<img class=\"pricing-card-image\"[^>]*>")]
        [InlineData("/Services/plumbing/tap-repairs", "<img class=\"pricing-card-image\"[^>]*>")]
        public async Task ServiceCardsOfferThePicturesMiddleCopy(string path, string cardPattern)
        {
            var client = this.server.CreateClient();
            var page = await client.GetStringAsync(path);

            var card = Regex.Match(page, cardPattern, RegexOptions.Singleline).Value;
            Assert.NotEmpty(card);
            var picture = Regex.Match(card, "src=\"([^\"?]+)").Groups[1].Value;
            Assert.EndsWith("-hero.webp", picture);
            Assert.Contains($"srcset=\"{picture[..^".webp".Length]}-640.webp 640w, {picture} 1024w\"", card);
            Assert.Matches("sizes=\"\\(max-width: 575\\.98px\\) calc\\(100vw - 48px\\), \\d+px\"", card);
        }

        // An area page sends a phone the strip from the middle of its main picture that a phone
        // shows; every area that comes with the site has one.
        [Fact]
        public async Task AreaPagesSendAPhoneTheStripOfTheMainPictureItShows()
        {
            var client = this.server.CreateClient();
            var page = await client.GetStringAsync("/Areas/cobham");

            var picture = Regex.Match(page, "<picture>.*?</picture>", RegexOptions.Singleline).Value;
            Assert.Contains("<source media=\"(max-width: 575.98px)\" srcset=\"/images/areas/cobham-hero-phone.webp\"", picture);
            Assert.Matches("<img src=\"/images/areas/cobham-hero\\.webp\\?v=[^\"]+\"[^>]*fetchpriority=\"high\"", picture);

            var webRoot = this.server.Services.GetRequiredService<IWebHostEnvironment>().WebRootPath;
            var pictures = Directory.GetFiles(Path.Combine(webRoot, "images", "areas"), "*-hero.webp");
            Assert.NotEmpty(pictures);
            Assert.All(pictures, area => Assert.True(File.Exists(area[..^".webp".Length] + "-phone.webp"), $"{Path.GetFileName(area)} has no phone strip"));
        }

        // A phone shows a narrow strip from the middle of the home page's main picture and is sent
        // that strip; the picture itself stays for wider screens and keeps its priority.
        [Fact]
        public async Task HomePageSendsAPhoneTheStripOfTheMainPictureItShows()
        {
            var client = this.server.CreateClient();
            var page = await client.GetStringAsync("/");

            var picture = Regex.Match(page, "<picture>.*?</picture>", RegexOptions.Singleline).Value;
            Assert.Contains("<source media=\"(max-width: 575.98px)\" srcset=\"/images/hero-phone.webp\"", picture);
            Assert.Matches("<img src=\"/images/hero\\.webp\\?v=[^\"]+\"[^>]*fetchpriority=\"high\"", picture);

            var webRoot = this.server.Services.GetRequiredService<IWebHostEnvironment>().WebRootPath;
            Assert.True(File.Exists(Path.Combine(webRoot, "images", "hero-phone.webp")));
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

        // A copy of the site that is not the live one says so across the top of a public page and
        // of an admin page, and in the tab's title: a test booking meant for staging was once made
        // on the live site (PROJECT_STATE.md Section 3cq). This host is Development; that the live
        // site shows neither is in EnvironmentMarkTests.
        [Fact]
        public async Task ACopyOfTheSiteSaysSoOnAPublicPageAndInTheAdminPanel()
        {
            var browser = this.server.CreateClient();
            await FormsWebTests.PostFormAsync(browser, "/Identity/Account/Login", new Dictionary<string, string>
            {
                ["Input.Email"] = SqliteWebApplicationFactory.AdminEmail,
                ["Input.Password"] = SqliteWebApplicationFactory.AdminPassword,
            });

            foreach (var url in new[] { "/", "/Administration/Dashboard" })
            {
                var response = await browser.GetAsync(url);
                response.EnsureSuccessStatusCode();
                Assert.Equal(url, response.RequestMessage.RequestUri.AbsolutePath);
                var content = await response.Content.ReadAsStringAsync();

                Assert.Contains("<div class=\"environment-strip\" role=\"note\">TEST SITE (development). Bookings, payments and emails here are not real.</div>", content);
                Assert.Contains("<title>[DEVELOPMENT] ", content);
            }
        }

        // On a laptop the admin menu shows its icons alone, and each name comes up on hover from
        // the link's title (PROJECT_STATE.md Section 3cq). A link added without one would be an
        // icon nobody can name.
        [Fact]
        public async Task EveryLinkInTheAdminMenuCarriesItsNameForTheFoldedMenu()
        {
            var browser = this.server.CreateClient();
            await FormsWebTests.PostFormAsync(browser, "/Identity/Account/Login", new Dictionary<string, string>
            {
                ["Input.Email"] = SqliteWebApplicationFactory.AdminEmail,
                ["Input.Password"] = SqliteWebApplicationFactory.AdminPassword,
            });

            var content = await (await browser.GetAsync("/Administration/Dashboard")).Content.ReadAsStringAsync();
            MatchCollection links = Regex.Matches(content, "<a class=\"admin-nav-link\"[^>]*>.*?</a>", RegexOptions.Singleline);

            Assert.Equal(8, links.Count);
            foreach (Match link in links)
            {
                var title = Regex.Match(link.Value, "title=\"([^\"]+)\"").Groups[1].Value;
                var label = Regex.Match(link.Value, "<span class=\"admin-nav-label\">([^<]+)</span>").Groups[1].Value;
                Assert.NotEmpty(title);
                Assert.Equal(title, label);
            }

            Assert.Contains("aria-controls=\"admin-sidebar\"", content);
        }

        [Theory]
        [MemberData(nameof(PublicPages))]
        public async Task PublicPagesLinkTheRealContactDetailsAndNeverThePlaceholderNumber(string url)
        {
            // The phone, WhatsApp link and email live once in GlobalConstants (PROJECT_STATE Section
            // 3bu). Before that a made-up number was typed by hand into 12 views in three spellings,
            // one of them an old-brand number the rename had missed. The header and footer are on
            // every page, so every page must carry the real links. The forms' example of what a
            // customer types is 07700 900123, from the range Ofcom keeps for made-up numbers, so
            // it can never be anyone's.
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
        [InlineData("/Services/small-building-works", "Small Building & Refurbishments")]
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

        // A category page told search engines nothing while the area pages did (PROJECT_STATE
        // Section 3cm). It describes itself as a service, with each service it lists at its own
        // address and price, the areas it covers, and its breadcrumb trail.
        [Fact]
        public async Task CategoryPageDescribesItsServicesToSearchEngines()
        {
            var client = this.server.CreateClient();
            var page = await client.GetStringAsync("/Services/plumbing");
            var blocks = JsonLdElements(page);

            var service = Assert.Single(blocks, b => b.GetProperty("@type").GetString() == "Service");
            Assert.Equal("Plumbing", service.GetProperty("serviceType").GetString());
            Assert.Equal("http://localhost/Services/plumbing", service.GetProperty("url").GetString());
            Assert.Contains(service.GetProperty("areaServed").EnumerateArray(), area => area.GetProperty("name").GetString() == "Chessington");

            var tapRepairs = Assert.Single(
                service.GetProperty("hasOfferCatalog").GetProperty("itemListElement").EnumerateArray(),
                offer => offer.GetProperty("itemOffered").GetProperty("name").GetString() == "Tap Repairs");
            Assert.Equal("http://localhost/Services/plumbing/tap-repairs", tapRepairs.GetProperty("itemOffered").GetProperty("url").GetString());
            Assert.Equal("90.00", tapRepairs.GetProperty("price").GetString());
            Assert.Equal("GBP", tapRepairs.GetProperty("priceCurrency").GetString());

            var breadcrumb = Assert.Single(blocks, b => b.GetProperty("@type").GetString() == "BreadcrumbList");
            Assert.Equal(
                new[] { "Home", "Services", "Plumbing" },
                breadcrumb.GetProperty("itemListElement").EnumerateArray().Select(item => item.GetProperty("name").GetString()));
        }

        // Building work is quoted after a survey and its cards show no price, so neither does what
        // the page tells a search engine.
        [Fact]
        public async Task QuoteOnlyCategoryPageGivesSearchEnginesNoPrice()
        {
            var client = this.server.CreateClient();
            var page = await client.GetStringAsync("/Services/small-building-works");
            var service = Assert.Single(JsonLdElements(page), b => b.GetProperty("@type").GetString() == "Service");

            var offers = service.GetProperty("hasOfferCatalog").GetProperty("itemListElement").EnumerateArray().ToList();
            Assert.NotEmpty(offers);
            Assert.All(offers, offer => Assert.False(offer.TryGetProperty("price", out _)));
        }

        // A service page shows its trail (Services, the category, the service); search engines get
        // the same trail from the home page down, as on the area and category pages.
        [Fact]
        public async Task ServicePageGivesSearchEnginesItsBreadcrumb()
        {
            var client = this.server.CreateClient();
            var page = await client.GetStringAsync("/Services/plumbing/tap-repairs");
            var breadcrumb = Assert.Single(JsonLdElements(page), b => b.GetProperty("@type").GetString() == "BreadcrumbList");

            var items = breadcrumb.GetProperty("itemListElement").EnumerateArray().ToList();
            Assert.Equal(new[] { "Home", "Services", "Plumbing", "Tap Repairs" }, items.Select(item => item.GetProperty("name").GetString()));
            Assert.Equal(
                new[] { "http://localhost/", "http://localhost/Services", "http://localhost/Services/plumbing", "http://localhost/Services/plumbing/tap-repairs" },
                items.Select(item => item.GetProperty("item").GetString()));
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
        [InlineData("plumbing", "General Plumbing Maintenance")]
        [InlineData("handyman", "General Handyman Call-Out")]
        public async Task BookingPageListsEachCategorysGeneralServiceFirst(string category, string expected)
        {
            // The booking page opens a category on the first service in its list. Handyman once
            // had no general service and opened on a bath screen fitting (PROJECT_STATE Section
            // 3bz); the general ones are now first by display order, not by a match on the word
            // "General" (Section 3ca).
            var client = this.server.CreateClient();
            var response = await client.GetAsync("/Booking?categorySlug=" + category);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();

            var firstInCategory = Regex.Match(content, "data-category=\"" + category + "\"[^>]*>\\s*([^<]+?)\\s*\\(from");
            Assert.True(firstInCategory.Success);
            Assert.Equal(expected, WebUtility.HtmlDecode(firstInCategory.Groups[1].Value));
        }

        [Fact]
        public async Task HomePagePopularServicesAreTheFourMarkedPopular()
        {
            // The grid showed the first four services in the alphabet. It now shows the four
            // marked popular, and a building job, which is quoted and not booked by the hour,
            // says so: no duration pill and no "Book" (PROJECT_STATE Section 3ca).
            var client = this.server.CreateClient();
            var response = await client.GetAsync("/");
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();

            var cards = Regex.Matches(content, "<a class=\"bento-card\"[\\s\\S]*?</a>").Select(m => WebUtility.HtmlDecode(m.Value)).ToList();
            var names = cards.Select(c => Regex.Match(c, "bento-card-title[^>]*>([^<]+)<").Groups[1].Value.Trim()).ToList();

            Assert.Equal(
                new[] { "Emergency Plumbing", "Full Bathroom Refurbishment", "Furniture Assembly", "Kitchen Fitting & Alterations" },
                names);

            foreach (var card in cards)
            {
                var action = Regex.Replace(Regex.Match(card, "bento-action-pill\">\\s*([^<]+)<").Groups[1].Value, "\\s+", " ").Trim();
                if (card.Contains("small-building-works"))
                {
                    Assert.Equal("Request Quote", action);
                    Assert.DoesNotContain("bento-duration-pill", card);
                }
                else
                {
                    Assert.Equal("Book", action);
                    Assert.Contains("bento-duration-pill", card);
                }
            }
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
            Assert.DoesNotContain("@import", css); // the web fonts are linked from the page head, and no stylesheet of ours is an import
            Assert.Contains(".site-logo", css); // a file from the middle of the list
            Assert.Contains(".material-symbols-filled", css); // the last file
        }

        [Theory]
        [MemberData(nameof(PublicPages))]
        public async Task PagesLoadTheirFontsFromTheSiteItself(string path)
        {
            // Loaded from Google, each font cost a stylesheet from one server and the font from
            // another before the page could show (PROJECT_STATE Section 3cz).
            var client = this.server.CreateClient();
            var response = await client.GetAsync(path);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();

            Assert.DoesNotContain("fonts.googleapis.com", content);
            Assert.DoesNotContain("fonts.gstatic.com", content);

            // Both preloaded fonts are the same address the font rules use, version and all, or
            // the browser fetches each one twice.
            var preloaded = Regex.Matches(content, "<link rel=\"preload\" href=\"([^\"]+)\" as=\"font\" type=\"font/woff2\" crossorigin")
                .Select(m => m.Groups[1].Value)
                .ToList();
            Assert.Equal(2, preloaded.Count);
            foreach (var font in preloaded)
            {
                Assert.Matches("^/fonts/[a-z-]+\\.woff2\\?v=[\\w-]+$", font);
                Assert.Contains($"src: url({font})", content);
            }
        }

        [Fact]
        public async Task TheFontFilesAreServedForAYear()
        {
            var client = this.server.CreateClient();
            var page = await (await client.GetAsync("/")).Content.ReadAsStringAsync();
            var fonts = Regex.Matches(page, "src: url\\((/fonts/[^)]+)\\)").Select(m => m.Groups[1].Value).ToList();
            Assert.Equal(3, fonts.Count);

            foreach (var font in fonts)
            {
                var response = await client.GetAsync(font);
                response.EnsureSuccessStatusCode();
                Assert.Equal("font/woff2", response.Content.Headers.ContentType?.MediaType);
                Assert.True(response.Headers.CacheControl?.MaxAge >= TimeSpan.FromDays(365));
            }
        }

        [Fact]
        public void EveryIconOnThePagesIsInTheIconFont()
        {
            // The icon font holds only the icons in tools/fonts/icon-names.txt. One written into a
            // page and left off that list would show as its name ("arrow_upward"), with nothing
            // to say so. Names picked in code are on the list by hand; this cannot see them.
            var contentRoot = this.server.Services.GetRequiredService<IWebHostEnvironment>().ContentRootPath;
            var listFile = Path.Combine(contentRoot, "..", "..", "..", "tools", "fonts", "icon-names.txt");
            var inFont = File.ReadAllLines(listFile)
                .Select(line => line.Trim())
                .Where(line => line.Length > 0 && !line.StartsWith('#'))
                .ToHashSet(StringComparer.Ordinal);

            var used = Directory
                .EnumerateFiles(contentRoot, "*.cshtml", SearchOption.AllDirectories)
                .Concat(Directory.EnumerateFiles(Path.Combine(contentRoot, "wwwroot", "js"), "*.js", SearchOption.AllDirectories))
                .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
                .SelectMany(file => Regex.Matches(File.ReadAllText(file), "material-symbols-(?:outlined|filled)[^>]*>\\s*([a-z0-9_]+)\\s*<")
                    .Select(m => m.Groups[1].Value))
                .ToHashSet(StringComparer.Ordinal);

            Assert.True(used.Count > 50, $"only {used.Count} icons found: is the pattern still right?");
            var missing = used.Where(name => !inFont.Contains(name)).OrderBy(name => name, StringComparer.Ordinal).ToList();
            Assert.True(
                missing.Count == 0,
                $"Not in the icon font: {string.Join(", ", missing)}. Add them to tools/fonts/icon-names.txt and run fetch-fonts.mjs there.");
        }

        // The JSON-LD blocks of a page. The "+" in the tag's type is rendered as an entity, which
        // browsers decode inside an attribute, so both spellings are matched.
        private static IEnumerable<string> JsonLdBlocks(string html)
        {
            return Regex.Matches(html, "<script type=\"application/ld(?:\\+|&#x2B;)json\">(.*?)</script>", RegexOptions.Singleline)
                .Select(match => match.Groups[1].Value);
        }

        // The JSON-LD blocks of a page, parsed, each copied out of its document before the
        // document is let go.
        private static List<JsonElement> JsonLdElements(string html)
        {
            return JsonLdBlocks(html)
                .Select(block =>
                {
                    using var json = JsonDocument.Parse(block);
                    return json.RootElement.Clone();
                })
                .ToList();
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
