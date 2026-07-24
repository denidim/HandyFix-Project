namespace HandyFix.Web.Tests
{
    using System.Net;
    using System.Threading.Tasks;

    using Microsoft.AspNetCore.Mvc.Testing;

    using Xunit;

    public class WebTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> server;

        public WebTests(WebApplicationFactory<Program> server)
        {
            this.server = server;
        }

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
    }
}
