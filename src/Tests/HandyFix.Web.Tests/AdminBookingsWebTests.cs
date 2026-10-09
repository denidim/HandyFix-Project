namespace HandyFix.Web.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    using HandyFix.Common;
    using HandyFix.Data;
    using HandyFix.Data.Models;
    using HandyFix.Services.Messaging;

    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Mvc.Testing;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;

    using Xunit;

    // A website booking through the whole stack, on a relational database as the live site has:
    // which emails go out and when, and what the admin's booking page offers and answers
    // (PROJECT_STATE.md Section 3ce). Each test has a site and a database of its own, because
    // each one takes a slot and counts the emails sent.
    public class AdminBookingsWebTests
    {
        private const string TechnicianEmailSubject = "Your technician for your Plumbing Handyman Surrey booking";

        // The email that used to go out before paying promised a visit that was dropped fifteen
        // minutes later if the deposit never came. Followed here one step at a time, as a browser
        // goes: the form, the payment, the way back.
        [Fact]
        public async Task ACustomerIsEmailedNothingUntilTheDepositIsPaidAndThenOnce()
        {
            using var site = new Site();
            HttpClient customer = site.Browser(followRedirects: false);
            AvailabilitySlot slot = site.AFreeSlot();

            HttpResponseMessage sent = await FormsWebTests.PostFormAsync(customer, "/Booking", site.BookingFormFor(slot));

            Assert.Equal(HttpStatusCode.Redirect, sent.StatusCode);
            Assert.StartsWith("/Payment/Pay?bookingId=", sent.Headers.Location.OriginalString);
            Assert.Equal("Pending", site.Booking().Status.Name);
            Assert.Empty(site.Emails.Sent);

            HttpResponseMessage paid = await customer.GetAsync(sent.Headers.Location);
            HttpResponseMessage back = await customer.GetAsync(paid.Headers.Location);

            Booking booking = site.Booking();
            Assert.Equal("/Booking/Confirmed/" + booking.Id, back.Headers.Location.OriginalString);
            Assert.Equal("Approved", booking.Status.Name);
            Assert.Equal(2, site.Emails.Sent.Count);

            RecordingEmailSender.Email toCustomer = site.Emails.Sent.Single(e => e.To == "ada@example.com");
            Assert.Equal("Your Plumbing Handyman Surrey Booking is Confirmed!", toCustomer.Subject);
            Assert.Contains("<strong>Booking Reference:</strong> " + BookingReference.Short(booking.Id) + "</li>", toCustomer.Body);
            Assert.DoesNotContain(booking.Id.ToString(), toCustomer.Body);

            RecordingEmailSender.Email toCompany = site.Emails.Sent.Single(e => e.To == GlobalConstants.BusinessEmail);
            Assert.Equal("New Confirmed Booking - Ada Lovelace", toCompany.Subject);
        }

        // The page the customer lands on after paying. It showed all 36 characters of the
        // booking's id, a two-hour window for a one-hour slot, and a promise of a text message
        // from a line that cannot send one.
        [Fact]
        public async Task TheCustomersConfirmationPageShowsTheShortReferenceAndTheSlotsOwnHour()
        {
            using var site = new Site();
            AvailabilitySlot slot = site.AFreeSlot();

            Booking booking = await site.BookAndPayAsync(slot);

            var page = WebUtility.HtmlDecode(await (await site.Browser().GetAsync("/Booking/Confirmed/" + booking.Id)).Content.ReadAsStringAsync());
            Assert.Contains(">#" + BookingReference.Short(booking.Id) + "</p>", page);
            Assert.DoesNotContain(booking.Id.ToString(), page);
            Assert.Contains(">" + slot.StartTime.ToString("hh:mm tt") + " - " + slot.EndTime.ToString("hh:mm tt") + "</p>", page);
            Assert.DoesNotContain(slot.StartTime.AddHours(2).ToString("hh:mm tt"), page);
            Assert.DoesNotContain("text you", page);
            Assert.Contains("We'll email you your technician's name and phone number as soon as one is assigned.", page);
        }

        // The email naming the technician used to hang off the "Approve" button, which a paid
        // booking never showed, so it could not be sent. It goes out when the admin picks one,
        // and the page says it went.
        [Fact]
        public async Task PickingATechnicianOnAPaidBookingEmailsTheCustomerAndSaysSo()
        {
            using var site = new Site();
            Booking booking = await site.BookAndPayAsync(site.AFreeSlot());
            HttpClient admin = await site.SignedInAdminAsync();
            var bookingPage = "/Administration/Bookings/Details/" + booking.Id;

            var opened = WebUtility.HtmlDecode(await (await admin.GetAsync(bookingPage)).Content.ReadAsStringAsync());
            Assert.Contains("<select name=\"technicianId\"", opened);
            Assert.Contains("Saving a new technician emails the customer that technician's name and phone number.", opened);
            Assert.Contains(">Deposit paid</span>", opened);
            Assert.Contains("Booking #" + BookingReference.Short(booking.Id), opened);
            Assert.Contains("<span>" + UkTime.FromUtc(booking.CreatedOn).ToString("hh:mm tt") + "</span>", opened);
            Assert.DoesNotContain("Approve Booking", opened);
            Assert.DoesNotContain("/Bookings/Approve", opened);

            HttpResponseMessage picked = await PostFromAsync(admin, bookingPage, "/Administration/Bookings/AssignTechnician", new Dictionary<string, string>
            {
                ["id"] = booking.Id.ToString(),
                ["technicianId"] = site.LaunchTechnicianId().ToString(),
            });

            var answered = WebUtility.HtmlDecode(await picked.Content.ReadAsStringAsync());
            Assert.Equal(bookingPage, picked.RequestMessage.RequestUri.AbsolutePath);
            Assert.Contains("Zapryan is now the technician for this booking. The customer has been emailed the name and phone number.", answered);

            // The technician is read in the job's own details, with the number to reach them on,
            // and not only in the picker that changes it.
            Assert.Matches("id=\"booking-technician\">\\s*<label[^>]*>Technician</label>\\s*<p[^>]*>\\s*<span[^>]*>engineering</span>\\s*<span>Zapryan</span>", answered);
            Assert.Matches("id=\"booking-technician\">[\\s\\S]*?<span>" + Regex.Escape(GlobalConstants.BusinessPhone) + "</span>", answered);
            Assert.Matches("id=\"booking-technician\">[\\s\\S]*?<span>Not picked yet</span>", opened);

            RecordingEmailSender.Email email = Assert.Single(site.Emails.Sent, e => e.Subject == TechnicianEmailSubject);
            Assert.Equal("ada@example.com", email.To);
            Assert.Contains("<strong>Zapryan</strong>", email.Body);
            Assert.Contains("<a href=\"tel:" + GlobalConstants.BusinessPhone.Replace(" ", string.Empty) + "\">" + GlobalConstants.BusinessPhone + "</a>", email.Body);
            Assert.Contains(BookingReference.Short(booking.Id), email.Body);
            Assert.Contains(site.ServiceName, email.Body);
            Assert.Contains("1 Analytical Engine Way, Chessington, KT9 1AA", email.Body);

            // Saved again as it stands: nothing to tell the customer a second time.
            HttpResponseMessage again = await PostFromAsync(admin, bookingPage, "/Administration/Bookings/AssignTechnician", new Dictionary<string, string>
            {
                ["id"] = booking.Id.ToString(),
                ["technicianId"] = site.LaunchTechnicianId().ToString(),
            });

            Assert.Contains("Nothing was changed: Zapryan was already the technician. No email was sent.", WebUtility.HtmlDecode(await again.Content.ReadAsStringAsync()));
            Assert.Single(site.Emails.Sent, e => e.Subject == TechnicianEmailSubject);
        }

        // An unpaid booking is dropped after about fifteen minutes, so there is no job to send
        // anyone to. The page offers no picker and no "Complete"; a picker sent from a page that
        // was opened earlier is refused, and the customer is not emailed.
        [Fact]
        public async Task ABookingWaitingForItsDepositHasNoPickerAndRefusesOneSentAnyway()
        {
            using var site = new Site();
            HttpClient customer = site.Browser(followRedirects: false);
            await FormsWebTests.PostFormAsync(customer, "/Booking", site.BookingFormFor(site.AFreeSlot()));
            Booking booking = site.Booking();
            HttpClient admin = await site.SignedInAdminAsync();
            var bookingPage = "/Administration/Bookings/Details/" + booking.Id;

            var opened = WebUtility.HtmlDecode(await (await admin.GetAsync(bookingPage)).Content.ReadAsStringAsync());
            Assert.DoesNotContain("name=\"technicianId\"", opened);
            Assert.Contains("A technician can be picked once the deposit is paid.", opened);
            Assert.Contains(">Not paid</span>", opened);
            Assert.DoesNotContain("Complete Service", opened);
            Assert.Contains("It can be marked as completed once its deposit is paid.", opened);
            Assert.Contains("Cancel Booking", opened);

            HttpResponseMessage picked = await PostFromAsync(admin, bookingPage, "/Administration/Bookings/AssignTechnician", new Dictionary<string, string>
            {
                ["id"] = booking.Id.ToString(),
                ["technicianId"] = site.LaunchTechnicianId().ToString(),
            });

            Assert.Contains("Nothing was changed. A technician can be picked once the deposit is paid", WebUtility.HtmlDecode(await picked.Content.ReadAsStringAsync()));
            Assert.Null(site.Booking().TechnicianId);

            HttpResponseMessage completed = await PostFromAsync(admin, bookingPage, "/Administration/Bookings/Complete", new Dictionary<string, string>
            {
                ["id"] = booking.Id.ToString(),
            });

            Assert.Contains("Nothing was changed. A booking can be marked as completed once its deposit is paid", WebUtility.HtmlDecode(await completed.Content.ReadAsStringAsync()));
            Assert.Equal("Pending", site.Booking().Status.Name);
            Assert.Empty(site.Emails.Sent);
        }

        // "Cancel Booking" used to be offered on a completed booking. Pressed there, it would
        // have relabelled a finished job and put its hour back on sale.
        [Fact]
        public async Task ACompletedBookingOffersNothingMoreAndCannotBeCancelled()
        {
            using var site = new Site();
            AvailabilitySlot slot = site.AFreeSlot();
            Booking booking = await site.BookAndPayAsync(slot);
            HttpClient admin = await site.SignedInAdminAsync();
            var bookingPage = "/Administration/Bookings/Details/" + booking.Id;
            var openedBefore = await (await admin.GetAsync(bookingPage)).Content.ReadAsStringAsync();

            HttpResponseMessage completed = await PostToAsync(admin, openedBefore, "/Administration/Bookings/Complete", new Dictionary<string, string>
            {
                ["id"] = booking.Id.ToString(),
            });

            var answered = WebUtility.HtmlDecode(await completed.Content.ReadAsStringAsync());
            Assert.Contains("The booking is marked as completed.", answered);
            Assert.Equal("Completed", site.Booking().Status.Name);
            Assert.DoesNotContain("Complete Service", answered);
            Assert.DoesNotContain("Cancel Booking", answered);
            Assert.DoesNotContain("name=\"technicianId\"", answered);
            Assert.Contains("Nothing more can be done on this booking.", answered);
            Assert.Contains("The technician can no longer be changed on this booking.", answered);

            // The page no longer has the button, so its form is sent from the copy of the page
            // that was opened before the booking was completed, as a second tab would send it.
            HttpResponseMessage cancelled = await PostToAsync(admin, openedBefore, "/Administration/Bookings/Cancel", new Dictionary<string, string>
            {
                ["id"] = booking.Id.ToString(),
            });

            Assert.Contains("Nothing was changed. This booking is already completed, cancelled or abandoned.", WebUtility.HtmlDecode(await cancelled.Content.ReadAsStringAsync()));
            Assert.Equal("Completed", site.Booking().Status.Name);
            Assert.True(site.Slot(slot.Id).IsBooked);
        }

        [Fact]
        public async Task CancellingAPaidBookingFreesItsSlotAndSaysTheCustomerHasNotBeenTold()
        {
            using var site = new Site();
            AvailabilitySlot slot = site.AFreeSlot();
            Booking booking = await site.BookAndPayAsync(slot);
            HttpClient admin = await site.SignedInAdminAsync();
            var bookingPage = "/Administration/Bookings/Details/" + booking.Id;
            var emailsBefore = site.Emails.Sent.Count;

            HttpResponseMessage cancelled = await PostFromAsync(admin, bookingPage, "/Administration/Bookings/Cancel", new Dictionary<string, string>
            {
                ["id"] = booking.Id.ToString(),
            });

            var answered = WebUtility.HtmlDecode(await cancelled.Content.ReadAsStringAsync());
            Assert.Contains("The booking is cancelled and its time slot can be booked again. The customer has not been emailed: please tell them yourself.", answered);
            Assert.Contains("refunded by hand in Stripe", answered);
            Assert.Equal("Cancelled", site.Booking().Status.Name);
            Assert.False(site.Slot(slot.Id).IsBooked);
            Assert.Equal(emailsBefore, site.Emails.Sent.Count);

            // The deposit was paid and stays shown as paid; nothing else can be done here.
            Assert.Contains(">Deposit paid</span>", answered);
            Assert.DoesNotContain("name=\"technicianId\"", answered);
            Assert.Contains("Nothing more can be done on this booking.", answered);
        }

        // The address the "Approve" button posted to. A booking is approved by its deposit.
        [Fact]
        public async Task TheApproveAddressIsGone()
        {
            using var site = new Site();
            Booking booking = await site.BookAndPayAsync(site.AFreeSlot());
            HttpClient admin = await site.SignedInAdminAsync();

            HttpResponseMessage approved = await PostFromAsync(admin, "/Administration/Bookings/Details/" + booking.Id, "/Administration/Bookings/Approve", new Dictionary<string, string>
            {
                ["id"] = booking.Id.ToString(),
            });

            Assert.Equal(HttpStatusCode.NotFound, approved.StatusCode);
        }

        // The list's middle card counted bookings "pending approval", a step that no longer
        // exists. It counts paid bookings nobody is on yet, which is the admin's next job.
        [Fact]
        public async Task TheBookingsListCountsPaidBookingsThatStillNeedATechnician()
        {
            using var site = new Site();
            Booking booking = await site.BookAndPayAsync(site.AFreeSlot());
            HttpClient admin = await site.SignedInAdminAsync();

            var list = WebUtility.HtmlDecode(await (await admin.GetAsync("/Administration/Bookings")).Content.ReadAsStringAsync());
            Assert.Matches("Waiting for a Technician</p>\\s*<h3[^>]*>1</h3>", list);
            Assert.DoesNotContain("Pending Approval", list);
            Assert.Contains(">#" + BookingReference.Short(booking.Id) + "</td>", list);

            // The row the card counts says so in its Technician column.
            Assert.Contains(">Needs one</span>", list);

            await PostFromAsync(admin, "/Administration/Bookings/Details/" + booking.Id, "/Administration/Bookings/AssignTechnician", new Dictionary<string, string>
            {
                ["id"] = booking.Id.ToString(),
                ["technicianId"] = site.LaunchTechnicianId().ToString(),
            });

            list = WebUtility.HtmlDecode(await (await admin.GetAsync("/Administration/Bookings")).Content.ReadAsStringAsync());
            Assert.Matches("Waiting for a Technician</p>\\s*<h3[^>]*>0</h3>", list);
            Assert.DoesNotContain(">Needs one</span>", list);
            Assert.Contains("<span class=\"text-primary\">Zapryan</span>", list);
        }

        // Sends one of the forms on a page to its own address, with the page's hidden fields
        // (the antiforgery token among them) as a browser would send them back.
        private static async Task<HttpResponseMessage> PostFromAsync(HttpClient browser, string page, string action, Dictionary<string, string> fields)
        {
            var html = await (await browser.GetAsync(page)).Content.ReadAsStringAsync();
            return await PostToAsync(browser, html, action, fields);
        }

        // The same, from a page already in hand.
        private static async Task<HttpResponseMessage> PostToAsync(HttpClient browser, string html, string action, Dictionary<string, string> fields)
        {
            var form = new Dictionary<string, string>(FormsWebTests.HiddenFieldsOf(html));
            Assert.Contains("__RequestVerificationToken", form.Keys);

            foreach (KeyValuePair<string, string> field in fields)
            {
                form[field.Key] = field.Value;
            }

            return await browser.PostAsync(action, new FormUrlEncodedContent(form));
        }

        // A site of its own for one test: its own database, an email sender that keeps what it
        // is given, and the pretend payment a machine with no Stripe key uses.
        private sealed class Site : IDisposable
        {
            private readonly SqliteWebApplicationFactory root = new SqliteWebApplicationFactory();
            private readonly WebApplicationFactory<Program> host;

            public Site()
            {
                this.host = this.root.WithWebHostBuilder(builder =>
                {
                    // A Development host also reads the developer's own user secrets. A Stripe
                    // key kept there would send these tests to Stripe itself.
                    builder.ConfigureAppConfiguration((context, configuration) => configuration.AddInMemoryCollection(
                        new Dictionary<string, string> { ["Stripe:SecretKey"] = string.Empty }));

                    builder.ConfigureServices(services => services.AddSingleton<IEmailSender>(this.Emails));
                });
            }

            public RecordingEmailSender Emails { get; } = new RecordingEmailSender();

            public string ServiceName { get; private set; }

            // A browser of its own: it keeps its cookies and asks for pages.
            public HttpClient Browser(bool followRedirects = true)
            {
                HttpClient client = this.host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = followRedirects });
                client.DefaultRequestHeaders.Accept.ParseAdd("text/html");
                return client;
            }

            public async Task<HttpClient> SignedInAdminAsync()
            {
                HttpClient browser = this.Browser();

                await FormsWebTests.PostFormAsync(browser, "/Identity/Account/Login", new Dictionary<string, string>
                {
                    ["Input.Email"] = SqliteWebApplicationFactory.AdminEmail,
                    ["Input.Password"] = SqliteWebApplicationFactory.AdminPassword,
                });

                return browser;
            }

            // The first free hour from the day after tomorrow on, out of the two weeks a new
            // site outside production starts with.
            public AvailabilitySlot AFreeSlot()
            {
                return this.Read(dbContext => dbContext.AvailabilitySlots
                    .Where(x => !x.IsBooked && !x.IsBlocked && x.StartTime >= DateTime.Today.AddDays(2))
                    .OrderBy(x => x.StartTime)
                    .First());
            }

            public AvailabilitySlot Slot(Guid id)
            {
                return this.Read(dbContext => dbContext.AvailabilitySlots.Single(x => x.Id == id));
            }

            // The one booking this site has, read afresh.
            public Booking Booking()
            {
                return this.Read(dbContext => dbContext.Bookings.Include(x => x.Status).Single());
            }

            public Guid LaunchTechnicianId()
            {
                return this.Read(dbContext => dbContext.Technicians.Single(x => x.FirstName == "Zapryan").Id);
            }

            // The booking form as a customer in a district we cover fills it in.
            public Dictionary<string, string> BookingFormFor(AvailabilitySlot slot)
            {
                Service service = this.Read(dbContext => dbContext.Services
                    .Where(x => x.Category.Slug == "plumbing")
                    .OrderBy(x => x.Name)
                    .First());
                this.ServiceName = service.Name;

                return new Dictionary<string, string>
                {
                    ["CustomerFirstName"] = "Ada",
                    ["CustomerLastName"] = "Lovelace",
                    ["Email"] = "ada@example.com",
                    ["PhoneNumber"] = "07700 900123",
                    ["Address"] = "1 Analytical Engine Way, Chessington",
                    ["Postcode"] = "KT9 1AA",
                    ["ProblemDescription"] = "The kitchen tap has been dripping for a week.",
                    ["SlotId"] = slot.Id.ToString(),
                    ["ServiceId"] = service.Id.ToString(),
                };
            }

            // Books the slot and pays its deposit as a customer's browser does, redirects and all.
            public async Task<Booking> BookAndPayAsync(AvailabilitySlot slot)
            {
                HttpResponseMessage landed = await FormsWebTests.PostFormAsync(this.Browser(), "/Booking", this.BookingFormFor(slot));

                Booking booking = this.Booking();
                Assert.Equal("/Booking/Confirmed/" + booking.Id, landed.RequestMessage.RequestUri.AbsolutePath);
                Assert.Equal("Approved", booking.Status.Name);
                return booking;
            }

            public void Dispose()
            {
                this.host.Dispose();
                this.root.Dispose();
            }

            private T Read<T>(Func<ApplicationDbContext, T> query)
            {
                using IServiceScope scope = this.host.Services.CreateScope();
                return query(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
            }
        }
    }
}
