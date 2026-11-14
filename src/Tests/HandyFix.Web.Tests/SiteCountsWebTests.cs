namespace HandyFix.Web.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Net;
    using System.Net.Http;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    using HandyFix.Web.Services.Counts;

    using Microsoft.AspNetCore.Mvc.Testing;

    using Xunit;

    // The website's count of what its visitors do, through the whole stack: what a page's script
    // sends is on the admin Dashboard, and the business's own browsers are left out
    // (PROJECT_STATE.md Section 3fa).
    public class SiteCountsWebTests : IClassFixture<SqliteWebApplicationFactory>
    {
        private const string Chrome = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36";

        private const string Android = "Mozilla/5.0 (Linux; Android 14; Pixel 8) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Mobile Safari/537.36";

        private const string HeadlessChrome = "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) HeadlessChrome/130.0.0.0 Safari/537.36";

        private readonly SqliteWebApplicationFactory server;

        public SiteCountsWebTests(SqliteWebApplicationFactory server)
        {
            this.server = server;
        }

        [Fact]
        public async Task WhatAVisitorsBrowserSendsIsOnTheDashboardAndOurOwnBrowsersAreLeftOut()
        {
            HttpClient admin = await this.SignedInAdminAsync();
            Dictionary<string, int> before = await ReadCountsAsync(admin);

            HttpClient onAComputer = this.Browser(Chrome);
            foreach (var what in new[] { "visit", "booking", "time", "call", "call", "whatsapp" })
            {
                // The page has no form and so no antiforgery token: the count takes none.
                Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(onAComputer, what)).StatusCode);
            }

            await SendAsync(this.Browser(Android), "visit");

            // None of these is a visitor: a browser driven by a script, a word that is no count,
            // the admin signed in, and a browser the admin panel has marked, signed out.
            await SendAsync(this.Browser(HeadlessChrome), "visit");
            await SendAsync(onAComputer, "everything");
            await SendAsync(admin, "visit");
            HttpClient marked = this.Browser(Chrome);
            marked.DefaultRequestHeaders.Add("Cookie", $"{SiteCounting.BrowserCookie}={SiteCounting.NotCounted}");
            await SendAsync(marked, "visit");

            string html = await DashboardAsync(admin);
            Dictionary<string, int> after = ReadCounts(html);
            Assert.Equal(before["visits"] + 2, after["visits"]);
            Assert.Equal(before["booking-opened"] + 1, after["booking-opened"]);
            Assert.Equal(before["time-picked"] + 1, after["time-picked"]);
            Assert.Equal(before["call-tapped"] + 2, after["call-tapped"]);
            Assert.Equal(before["whatsapp-tapped"] + 1, after["whatsapp-tapped"]);
            Assert.Matches("\\d+ on a phone, \\d+ on a computer", html);

            // No booking was made and no enquiry sent, and those rows come from the panel.
            Assert.Equal(before["bookings-sent"], after["bookings-sent"]);
            Assert.Equal(before["enquiries-sent"], after["enquiries-sent"]);
            Assert.Contains("The count began on", html);
        }

        [Fact]
        public async Task TheDashboardHasQuickActionsAboveTheTwoTablesAndEachTableKeepsItsOwnPeriod()
        {
            HttpClient admin = await this.SignedInAdminAsync();
            string html = await DashboardAsync(admin);

            int quickActions = html.IndexOf("Quick Actions", StringComparison.Ordinal);
            int visitors = html.IndexOf("What Visitors Did on the Website", StringComparison.Ordinal);
            int heardAbout = html.IndexOf("How Customers Heard About Us", StringComparison.Ordinal);
            Assert.True(quickActions > 0 && quickActions < visitors && visitors < heardAbout);

            foreach (var row in new[] { "Visits to the site", "Opened the booking page", "Picked a time", "Sent the booking", "Paid the deposit", "Tapped Call", "Tapped WhatsApp", "Sent an enquiry" })
            {
                Assert.Contains(row, html);
            }

            Assert.Contains("action=\"/Administration/Dashboard#dashboard-site-counts\"", html);
            Assert.Contains("What happened on the website in the last 30 days", html);

            string sinceTheStart = await DashboardAsync(admin, "?counts=all");
            Assert.Contains("<option value=\"all\" selected=\"selected\">Since the count began</option>", sinceTheStart);
            Assert.Contains("What happened on the website since the count began", sinceTheStart);

            // Changing the other table's period does not put this one back to 30 days.
            Assert.Contains("<input type=\"hidden\" name=\"counts\" value=\"all\" />", sinceTheStart);
            Assert.Contains("Every enquiry and every job of the last 30 days", sinceTheStart);
        }

        [Fact]
        public async Task ABrowserThatOpensThePanelIsMarkedAndTheButtonPutsItBack()
        {
            HttpClient admin = await this.SignedInAdminAsync();

            string html = await DashboardAsync(admin);
            Assert.Contains("This browser is not counted", html);
            Assert.Contains(">Count this browser</button>", html);

            Dictionary<string, string> form = FormsWebTests.HiddenFieldsOf(html);
            HttpResponseMessage pressed = await admin.PostAsync(
                "/Administration/Dashboard/CountThisBrowser",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["__RequestVerificationToken"] = form["__RequestVerificationToken"],
                    ["count"] = "true",
                }));
            Assert.True(pressed.IsSuccessStatusCode);

            string after = WebUtility.HtmlDecode(await pressed.Content.ReadAsStringAsync());
            Assert.Contains("This browser is counted like any visitor's", after);
            Assert.Contains(">Don't count this browser</button>", after);
        }

        private static Task<HttpResponseMessage> SendAsync(HttpClient browser, string what)
        {
            return browser.PostAsync("/count", new FormUrlEncodedContent(new Dictionary<string, string> { ["what"] = what }));
        }

        private static async Task<string> DashboardAsync(HttpClient admin, string query = "")
        {
            return WebUtility.HtmlDecode(await (await admin.GetAsync("/Administration/Dashboard" + query)).Content.ReadAsStringAsync());
        }

        private static async Task<Dictionary<string, int>> ReadCountsAsync(HttpClient admin)
        {
            return ReadCounts(await DashboardAsync(admin));
        }

        // Each figure's cell names what it counts (data-count), so a test reads the figure and
        // not its place in the table.
        private static Dictionary<string, int> ReadCounts(string html)
        {
            var counts = new Dictionary<string, int>();
            foreach (Match cell in Regex.Matches(html, "data-count=\"([a-z-]+)\">(\\d+)<"))
            {
                counts[cell.Groups[1].Value] = int.Parse(cell.Groups[2].Value, CultureInfo.InvariantCulture);
            }

            Assert.Equal(8, counts.Count);
            return counts;
        }

        // A client that keeps no cookies of its own, so a test can say which it carries.
        private HttpClient Browser(string userAgent)
        {
            HttpClient browser = this.server.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
            browser.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", userAgent);
            return browser;
        }

        private async Task<HttpClient> SignedInAdminAsync()
        {
            HttpClient admin = this.server.CreateClient();
            admin.DefaultRequestHeaders.Accept.ParseAdd("text/html");
            admin.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", Chrome);
            await FormsWebTests.PostFormAsync(admin, "/Identity/Account/Login", new Dictionary<string, string>
            {
                ["Input.Email"] = SqliteWebApplicationFactory.AdminEmail,
                ["Input.Password"] = SqliteWebApplicationFactory.AdminPassword,
            });

            return admin;
        }
    }
}
