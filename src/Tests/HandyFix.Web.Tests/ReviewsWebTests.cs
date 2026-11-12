namespace HandyFix.Web.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;

    using HandyFix.Data;
    using HandyFix.Data.Models;

    using Microsoft.Extensions.DependencyInjection;

    using Xunit;

    // Our Google reviews through the whole stack: typed in on the admin's form, then shown as text
    // on /Reviews, the home page and the page of their own town, with no review data for search
    // engines (PROJECT_STATE.md Section 3eh).
    public class ReviewsWebTests : IClassFixture<SqliteWebApplicationFactory>
    {
        private const string ReviewsAdmin = "/Administration/Reviews";

        private readonly SqliteWebApplicationFactory server;

        public ReviewsWebTests(SqliteWebApplicationFactory server)
        {
            this.server = server;
        }

        [Fact]
        public async Task AReviewTypedInShowsOnTheReviewsPageTheHomePageAndItsTownsPageOnly()
        {
            (string Name, string Slug)[] areas = this.TwoAreas();
            HttpClient browser = await this.SignedInBrowserAsync();

            HttpResponseMessage added = await FormsWebTests.PostFormAsync(browser, ReviewsAdmin + "/Create", new Dictionary<string, string>
            {
                ["CustomerName"] = "Sarah K.",
                ["Town"] = areas[0].Name,
                ["JobDone"] = "Boiler service",
                ["Rating"] = "5",
                ["Comment"] = "Came the same day and fixed the boiler in an hour.",
                ["ReviewedOn"] = "2026-11-06",
            });

            string list = WebUtility.HtmlDecode(await added.Content.ReadAsStringAsync());
            Assert.Equal(ReviewsAdmin, added.RequestMessage.RequestUri.AbsolutePath);
            Assert.Contains("Sarah K.'s review was added. It is on the website now.", list);

            string reviews = WebUtility.HtmlDecode(await this.GetAsync("/Reviews"));
            Assert.Contains("Came the same day and fixed the boiler in an hour.", reviews);
            Assert.Contains(areas[0].Name + " · Boiler service", reviews);
            Assert.Contains("Google review, November 2026", reviews);
            Assert.Contains(">SK</span>", reviews);
            Assert.Contains("Leave a Google Review", reviews);

            string home = WebUtility.HtmlDecode(await this.GetAsync("/"));
            Assert.Contains("Came the same day and fixed the boiler in an hour.", home);

            string ownTown = WebUtility.HtmlDecode(await this.GetAsync("/Areas/" + areas[0].Slug));
            Assert.Contains("What Our Customers in " + areas[0].Name + " Say", ownTown);
            Assert.Contains("Came the same day and fixed the boiler in an hour.", ownTown);

            string otherTown = WebUtility.HtmlDecode(await this.GetAsync("/Areas/" + areas[1].Slug));
            Assert.DoesNotContain("Came the same day", otherTown);
            Assert.DoesNotContain("What Our Customers in", otherTown);

            // Google ignores or penalises stars a business serves about itself, so none of the
            // pages carries review data for search engines, only the text people read.
            foreach (string page in new[] { reviews, home, ownTown })
            {
                Assert.DoesNotContain("aggregateRating", page);
                Assert.DoesNotContain("ratingValue", page);
                Assert.DoesNotMatch("\"@type\"\\s*:\\s*\"Review\"", page);
                Assert.DoesNotContain("schema.org/Review", page);
            }
        }

        [Fact]
        public async Task TheEditFormChangesTheReviewOnThePage()
        {
            HttpClient browser = await this.SignedInBrowserAsync();
            Guid id;
            using (IServiceScope scope = this.server.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var review = new Review { CustomerName = "Tom B.", Comment = "Shelves put up straight.", Rating = 4, IsApproved = true, ReviewedOn = new DateTime(2026, 11, 8) };
                db.Reviews.Add(review);
                await db.SaveChangesAsync();
                id = review.Id;
            }

            await FormsWebTests.PostFormAsync(browser, ReviewsAdmin + "/Edit/" + id, new Dictionary<string, string>
            {
                ["CustomerName"] = "Tom B.",
                ["Town"] = string.Empty,
                ["JobDone"] = "Shelves",
                ["Rating"] = "5",
                ["Comment"] = "Shelves put up straight, and quickly.",
                ["ReviewedOn"] = "2026-11-08",
            });

            string reviews = WebUtility.HtmlDecode(await this.GetAsync("/Reviews"));
            Assert.Contains("Shelves put up straight, and quickly.", reviews);
            Assert.Contains(">Shelves</p>", reviews);
        }

        private (string Name, string Slug)[] TwoAreas()
        {
            using IServiceScope scope = this.server.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            return db.ServiceAreas.OrderBy(x => x.Name).Take(2).AsEnumerable().Select(x => (x.Name, x.Slug)).ToArray();
        }

        private async Task<string> GetAsync(string url)
        {
            HttpResponseMessage response = await this.server.CreateClient().GetAsync(url);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
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
