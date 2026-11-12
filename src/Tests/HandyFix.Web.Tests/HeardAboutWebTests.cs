namespace HandyFix.Web.Tests
{
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    using Xunit;

    // "How did you hear about us?" on the website's two forms, optional on both, and the
    // Dashboard's table of the answers (PROJECT_STATE.md Section 3ej).
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

            Assert.Contains("How Customers Heard About Us, Last 30 Days", html);
            Assert.Contains(">A friend or neighbour</td>", html);
            Assert.Contains(">Not said</td>", html);
        }
    }
}
