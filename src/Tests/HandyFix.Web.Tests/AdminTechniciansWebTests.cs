namespace HandyFix.Web.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;

    using HandyFix.Common;
    using HandyFix.Data;
    using HandyFix.Data.Models;
    using HandyFix.Services.Data.Bookings;
    using HandyFix.Web.ViewModels.Booking;

    using Microsoft.Extensions.DependencyInjection;

    using Xunit;

    // The technician roster through the whole stack, on a relational database as the live site
    // has: what a new database starts with, and a technician kept under a first name alone
    // (PROJECT_STATE.md Section 3cd).
    public class AdminTechniciansWebTests : IClassFixture<SqliteWebApplicationFactory>
    {
        private const string RosterPage = "/Administration/Technicians";

        private readonly SqliteWebApplicationFactory server;

        public AdminTechniciansWebTests(SqliteWebApplicationFactory server)
        {
            this.server = server;
        }

        // The live site's database is a new one, like this host's. It used to start with
        // "John Doe" on a made-up number, ready to be assigned to a real booking.
        [Fact]
        public async Task ANewSiteStartsWithTheLaunchTechnicianAndNoMadeUpOne()
        {
            HttpClient browser = await this.SignedInBrowserAsync();

            var roster = WebUtility.HtmlDecode(await (await browser.GetAsync(RosterPage)).Content.ReadAsStringAsync());

            Assert.Contains(">Zapryan</p>", roster);
            Assert.Contains(">" + GlobalConstants.BusinessPhone + "</a>", roster);
            Assert.DoesNotContain("John Doe", roster);
            Assert.DoesNotContain("07123456789", roster);
        }

        // The form used to refuse a technician without a last name. Sent here as a browser sends
        // it, the empty box included, and then followed to each place the name is shown.
        [Fact]
        public async Task TheFormTakesATechnicianWithNoLastNameAndTheNameStandsAloneWhereverItIsShown()
        {
            HttpClient browser = await this.SignedInBrowserAsync();

            HttpResponseMessage added = await FormsWebTests.PostFormAsync(browser, RosterPage + "/Create", new Dictionary<string, string>
            {
                ["FirstName"] = "Sam",
                ["LastName"] = string.Empty,
                ["PhoneNumber"] = "07700 900123",
                ["IsActive"] = "true",
            });

            var roster = WebUtility.HtmlDecode(await added.Content.ReadAsStringAsync());
            Assert.Equal(RosterPage, added.RequestMessage.RequestUri.AbsolutePath);
            Assert.Contains("Technician \"Sam\" was added.", roster);
            Assert.Contains(">Sam</p>", roster);

            Guid technicianId;
            Guid bookingId;
            using (IServiceScope scope = this.server.Services.CreateScope())
            {
                ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                Technician technician = dbContext.Technicians.Single(x => x.FirstName == "Sam");
                Assert.Null(technician.LastName);
                technicianId = technician.Id;

                var booking = new Booking
                {
                    CustomerFirstName = "Ada",
                    CustomerLastName = "Lovelace",
                    Email = "ada@example.com",
                    PhoneNumber = "07700 900123",
                    Address = "1 Analytical Engine Way, Chessington, KT9 1AA",
                    ProblemDescription = "The kitchen tap has been dripping for a week.",
                    StatusId = dbContext.BookingStatuses.Single(x => x.Name == "Pending").Id,
                    TechnicianId = technicianId,
                };
                dbContext.Bookings.Add(booking);
                await dbContext.SaveChangesAsync();
                bookingId = booking.Id;

                // The name as the query behind the bookings pages puts it together.
                BookingDetailsViewModel listed = await scope.ServiceProvider.GetRequiredService<IBookingsService>()
                    .GetByIdAsync<BookingDetailsViewModel>(bookingId);
                Assert.Equal("Sam", listed.TechnicianName);
            }

            var editPage = WebUtility.HtmlDecode(await (await browser.GetAsync(RosterPage + "/Edit/" + technicianId)).Content.ReadAsStringAsync());
            Assert.Contains("<span class=\"breadcrumb-current\">Sam</span>", editPage);

            var bookingPage = WebUtility.HtmlDecode(await (await browser.GetAsync("/Administration/Bookings/Details/" + bookingId)).Content.ReadAsStringAsync());
            Assert.Matches("<option value=\"" + technicianId + "\" selected=\"selected\">\\s*Sam\\s*</option>", bookingPage);
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
