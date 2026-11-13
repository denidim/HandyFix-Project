namespace HandyFix.Web.Tests
{
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    using Xunit;

    // "How did you hear about us?" on the website's two forms, optional on both, and the
    // Dashboard's table of the answers, enquiries and jobs apart (PROJECT_STATE.md Sections 3ej
    // and 3et).
    public class HeardAboutWebTests : IClassFixture<SqliteWebApplicationFactory>
    {
        private readonly SqliteWebApplicationFactory server;

        public HeardAboutWebTests(SqliteWebApplicationFactory server)
        {
            this.server = server;
        }

        [Theory]
        [InlineData("/Booking?categorySlug=plumbing")]
        [InlineData("/Contact")]
        public async Task BothFormsAskTheQuestionWithoutMakingItRequired(string url)
        {
            string html = WebUtility.HtmlDecode(await (await this.server.CreateClient().GetAsync(url)).Content.ReadAsStringAsync());

            Assert.Contains("How did you hear about us?", html);

            // The empty choice is the one picked, and it is the answer "Not said".
            Assert.Contains("<option value=\"NotSaid\" selected=\"selected\">Please choose (optional)</option>", html);
            Assert.Contains("<option value=\"GoogleMaps\">Google Maps</option>", html);
            Assert.Contains("<option value=\"FriendOrNeighbour\">A friend or neighbour</option>", html);
            string select = Regex.Match(html, "<select[^>]*name=\"HeardAbout\"[^>]*>").Value;
            Assert.NotEmpty(select);
            Assert.DoesNotMatch("\\srequired(\\s|>|=)", select);
        }

        [Fact]
        public async Task TheDashboardShowsTheTableOfAnswers()
        {
            HttpClient browser = this.server.CreateClient();
            browser.DefaultRequestHeaders.Accept.ParseAdd("text/html");
            await FormsWebTests.PostFormAsync(browser, "/Identity/Account/Login", new Dictionary<string, string>
            {
                ["Input.Email"] = SqliteWebApplicationFactory.AdminEmail,
                ["Input.Password"] = SqliteWebApplicationFactory.AdminPassword,
            });

            string html = WebUtility.HtmlDecode(await (await browser.GetAsync("/Administration/Dashboard")).Content.ReadAsStringAsync());

            Assert.Contains("How Customers Heard About Us", html);
            Assert.Contains(">A friend or neighbour</td>", html);
            Assert.Contains(">Not said</td>", html);

            // The enquiries and the jobs each have their column (Section 3et).
            Assert.Matches(">Answer</th>\\s*<th[^>]*>Enquiries</th>\\s*<th[^>]*>Jobs</th>", html);

            // The last 30 days unless "All time" is picked; the choice sends the page back to the table.
            Assert.Contains("action=\"/Administration/Dashboard#dashboard-heard-about\"", html);
            Assert.Contains("<option value=\"\">Last 30 days</option>", html);
            Assert.Contains("<option value=\"all\">All time</option>", html);
            Assert.Contains("Every enquiry and every job of the last 30 days", html);

            string allTime = WebUtility.HtmlDecode(await (await browser.GetAsync("/Administration/Dashboard?heardAbout=all")).Content.ReadAsStringAsync());
            Assert.Contains("<option value=\"all\" selected=\"selected\">All time</option>", allTime);
            Assert.Contains("Every enquiry and every job so far", allTime);
        }
    }
}
