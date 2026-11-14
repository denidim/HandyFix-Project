namespace HandyFix.Web.Tests
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    using Xunit;

    // The admin's Gallery page, through the whole app.
    public class AdminGalleryWebTests : IClassFixture<SqliteWebApplicationFactory>
    {
        private readonly SqliteWebApplicationFactory server;

        public AdminGalleryWebTests(SqliteWebApplicationFactory server)
        {
            this.server = server;
        }

        // An action on the admin's Gallery page brings the admin back to that page. The public
        // Gallery page had an address of its own, "/Gallery", which won whenever the admin's
        // controller sent the browser back to its Index: every upload, star, move, edit and delete
        // ended on the public page (PROJECT_STATE.md Section 3fd). The controller tests did not
        // see it, since they check the action's name, not the address the app builds from it.
        [Fact]
        public async Task AnActionOnTheAdminGalleryPageReturnsToIt()
        {
            HttpClient browser = await this.SignedInBrowserAsync();
            string page = await browser.GetStringAsync("/Administration/Gallery");

            // "Move down" on the last photo: it is answered like any move and changes nothing.
            string moveDown = Regex.Matches(page, "action=\"(/Administration/Gallery/Move/[0-9a-f-]+\\?direction=1)\"")
                .Select(m => m.Groups[1].Value)
                .Last();
            var form = new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = FormsWebTests.HiddenFieldsOf(page)["__RequestVerificationToken"],
            };

            HttpResponseMessage response = await browser.PostAsync(moveDown, new FormUrlEncodedContent(form));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("/Administration/Gallery", response.RequestMessage.RequestUri.AbsolutePath);
            Assert.Contains("Upload Job Photos", await response.Content.ReadAsStringAsync());
        }

        // The public page keeps its address, from the public pages and the admin's link to it alike.
        [Fact]
        public async Task ThePublicGalleryKeepsItsAddress()
        {
            HttpClient browser = await this.SignedInBrowserAsync();

            Assert.Contains("href=\"/Gallery\"", await browser.GetStringAsync("/"));
            Assert.Contains("href=\"/Gallery\"", await browser.GetStringAsync("/Administration/Gallery"));
            Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/Gallery")).StatusCode);
        }

        // A browser of its own, signed in as the admin this host seeds.
        private async Task<HttpClient> SignedInBrowserAsync()
        {
            HttpClient browser = this.server.CreateClient();
            browser.DefaultRequestHeaders.Accept.ParseAdd("text/html");

            await FormsWebTests.PostFormAsync(browser, "/Identity/Account/Login", new Dictionary<string, string>
            {
                ["Input.Email"] = SqliteWebApplicationFactory.AdminEmail,
                ["Input.Password"] = SqliteWebApplicationFactory.AdminPassword,
            });

            return browser;
        }
    }
}
