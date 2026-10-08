namespace HandyFix.Services.Data.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Data;
    using HandyFix.Data.Models;
    using HandyFix.Data.Repositories;
    using HandyFix.Services.Data.Availability;
    using HandyFix.Services.Data.Bookings;
    using HandyFix.Services.Data.Payments;
    using HandyFix.Services.Messaging;
    using HandyFix.Web.ViewModels.Booking;

    using Microsoft.AspNetCore.Hosting;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Diagnostics;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging.Abstractions;

    using Moq;

    using Xunit;

    // The three booking emails each follow something already saved: the booking, the deposit, the
    // approval. A send that fails must not undo or hide that, and what a customer typed must
    // reach an email as text (PROJECT_STATE.md Section 3cb).
    public class BookingEmailsTests
    {
        // An exception from the email used to reach the customer as "an error occurred while
        // saving your booking", for a booking that had been saved with its slot. A second try
        // then lost the slot to the first one.
        [Fact]
        public async Task CreateBookingAsyncShouldKeepTheBookingWhenItsEmailCannotBeSent()
        {
            using var world = new World();
            world.EmailsFail();

            Booking booking = await world.Bookings.CreateBookingAsync(world.BookingFor("12 Main Rd, Sutton"), new List<string>());

            Assert.NotNull(booking);
            Assert.Single(world.DbContext.Bookings);
            Assert.True(world.DbContext.AvailabilitySlots.Single().IsBooked);
        }

        [Fact]
        public async Task TheBookingReceivedEmailShouldCarryWhatWasTypedAsText()
        {
            using var world = new World();
            List<SentEmail> sent = world.CaptureEmails();

            await world.Bookings.CreateBookingAsync(world.BookingFor("<b>12 Main Rd</b> & Sons, Sutton"), new List<string>());

            SentEmail email = Assert.Single(sent);
            Assert.Equal("ada@example.com", email.To);
            Assert.Contains("&lt;b&gt;12 Main Rd&lt;/b&gt; &amp; Sons, Sutton, SM1 1AA", email.Body);
            Assert.DoesNotContain("<b>12 Main Rd</b>", email.Body);
        }

        // The customer has paid by now. A failing email used to end their return from the payment
        // page on an error page, and the company was not told either.
        [Fact]
        public async Task ProcessPaymentSuccessAsyncShouldMarkThePaymentPaidAndTellTheCompanyWhenTheCustomersEmailFails()
        {
            using var world = new World();
            var checkoutSessionId = await world.SeedPendingPaymentAsync("12 Main Rd, Sutton");
            world.EmailsFail(to: "ada@example.com");

            await world.Payments.ProcessPaymentSuccessAsync(checkoutSessionId, "txn_1");

            Payment payment = world.DbContext.Payments.Include(p => p.Status).Single();
            Assert.Equal("DepositPaid", payment.Status.Name);
            Assert.Equal("Approved", world.DbContext.Bookings.Include(b => b.Status).Single().Status.Name);
            world.EmailSender.Verify(
                x => x.SendEmailAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    "info@plumbing-handyman-surrey.co.uk",
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<IEnumerable<EmailAttachment>>(),
                    It.IsAny<string>()),
                Times.Once);
        }

        [Fact]
        public async Task TheDepositEmailsShouldCarryTextNotHtmlAndLetTheCompanyReplyToTheCustomer()
        {
            using var world = new World();
            var checkoutSessionId = await world.SeedPendingPaymentAsync("<a href=\"https://evil.example\">12 Main Rd</a>");
            List<SentEmail> sent = world.CaptureEmails();

            await world.Payments.ProcessPaymentSuccessAsync(checkoutSessionId, "txn_1");

            Assert.Equal(2, sent.Count);
            Assert.All(sent, email => Assert.DoesNotContain("<a href=\"https://evil.example\">", email.Body));
            Assert.All(sent, email => Assert.Contains("&lt;a href=&quot;https://evil.example&quot;&gt;12 Main Rd&lt;/a&gt;", email.Body));

            SentEmail toCustomer = sent.Single(e => e.To == "ada@example.com");
            Assert.Null(toCustomer.ReplyTo);

            // Pressing Reply on the company's notice answers the customer.
            SentEmail toCompany = sent.Single(e => e.To == "info@plumbing-handyman-surrey.co.uk");
            Assert.Equal("ada@example.com", toCompany.ReplyTo);
            Assert.Contains("Ada Lovelace", toCompany.Body);
        }

        // The status is saved before the "CONFIRMED" email goes. A failed send must not turn the
        // admin's Approve into an error page for a booking that was in fact approved.
        [Fact]
        public async Task UpdateStatusAsyncShouldApproveTheBookingWhenTheConfirmationEmailCannotBeSent()
        {
            using var world = new World();
            await world.SeedPendingPaymentAsync("12 Main Rd, Sutton");
            world.EmailsFail();

            await world.Bookings.UpdateStatusAsync(world.DbContext.Bookings.Single().Id, "Approved");

            Assert.Equal("Approved", world.DbContext.Bookings.Include(b => b.Status).Single().Status.Name);
        }

        // A technician may be on the roster under a first name alone, and a number is written
        // with spaces (PROJECT_STATE.md Section 3cd). The name must not drag a space behind it,
        // and the link's address holds the number without the spaces the text keeps.
        [Theory]
        [InlineData(null, "<strong>Zapryan</strong>")]
        [InlineData("Stone", "<strong>Zapryan Stone</strong>")]
        public async Task TheConfirmedEmailShouldNameTheTechnicianAndLinkTheirNumber(string lastName, string expectedName)
        {
            using var world = new World();
            await world.SeedPendingPaymentAsync("12 Main Rd, Sutton");

            var technician = new Technician { FirstName = "Zapryan", LastName = lastName, PhoneNumber = "020 3951 5915" };
            world.DbContext.Technicians.Add(technician);
            Booking booking = world.DbContext.Bookings.Single();
            booking.TechnicianId = technician.Id;
            await world.DbContext.SaveChangesAsync();
            List<SentEmail> sent = world.CaptureEmails();

            await world.Bookings.UpdateStatusAsync(booking.Id, "Approved");

            SentEmail email = Assert.Single(sent);
            Assert.Contains(expectedName, email.Body);
            Assert.Contains("<a href=\"tel:02039515915\">020 3951 5915</a>", email.Body);
        }

        private sealed class SentEmail
        {
            public string To { get; set; }

            public string Body { get; set; }

            public string ReplyTo { get; set; }
        }

        // A database with the statuses, one service and one free slot, and the two services
        // wired to it with a mocked email sender.
        private sealed class World : IDisposable
        {
            private readonly Service service;
            private readonly AvailabilitySlot slot;

            public World()
            {
                this.DbContext = new ApplicationDbContext(
                    new DbContextOptionsBuilder<ApplicationDbContext>()
                        .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                        .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                        .Options);

                this.DbContext.BookingStatuses.AddRange(
                    new BookingStatus { Name = "Pending" },
                    new BookingStatus { Name = "Approved" });
                this.DbContext.PaymentStatuses.AddRange(
                    new PaymentStatus { Name = "Pending" },
                    new PaymentStatus { Name = "DepositPaid" });

                var category = new ServiceCategory { Name = "Plumbing", Description = "Plumbing", Slug = "plumbing" };
                this.DbContext.ServiceCategories.Add(category);

                this.service = new Service { Name = "Leak Fix", Description = "Repair a leak", Slug = "leak-fix", BasePrice = 90.00m, CategoryId = category.Id };
                this.DbContext.Services.Add(this.service);

                this.slot = new AvailabilitySlot { StartTime = DateTime.Today.AddDays(1).AddHours(9), EndTime = DateTime.Today.AddDays(1).AddHours(10) };
                this.DbContext.AvailabilitySlots.Add(this.slot);
                this.DbContext.SaveChanges();

                IConfiguration configuration = new ConfigurationBuilder().Build();
                var bookingRepo = new EfDeletableEntityRepository<Booking>(this.DbContext);
                var bookingStatusRepo = new EfDeletableEntityRepository<BookingStatus>(this.DbContext);
                var slotRepo = new EfDeletableEntityRepository<AvailabilitySlot>(this.DbContext);

                this.Payments = new PaymentsService(
                    new EfDeletableEntityRepository<Payment>(this.DbContext),
                    new EfDeletableEntityRepository<PaymentStatus>(this.DbContext),
                    bookingRepo,
                    bookingStatusRepo,
                    this.EmailSender.Object,
                    configuration,
                    Mock.Of<IWebHostEnvironment>(),
                    NullLogger<PaymentsService>.Instance);

                this.Bookings = new BookingsService(
                    bookingRepo,
                    new EfDeletableEntityRepository<Service>(this.DbContext),
                    slotRepo,
                    bookingStatusRepo,
                    new EfDeletableEntityRepository<BookingImage>(this.DbContext),
                    new EfDeletableEntityRepository<Technician>(this.DbContext),
                    new AvailabilityService(slotRepo),
                    this.Payments,
                    new DbQueryRunner(this.DbContext),
                    this.EmailSender.Object,
                    configuration,
                    NullLogger<BookingsService>.Instance);
            }

            public ApplicationDbContext DbContext { get; }

            public Mock<IEmailSender> EmailSender { get; } = new Mock<IEmailSender>();

            public BookingsService Bookings { get; }

            public PaymentsService Payments { get; }

            public void Dispose() => this.DbContext.Dispose();

            public BookingInputModel BookingFor(string address) => new BookingInputModel
            {
                CustomerFirstName = "Ada",
                CustomerLastName = "Lovelace",
                Email = "ada@example.com",
                PhoneNumber = "07700 900123",
                Address = address,
                Postcode = "SM1 1AA",
                ProblemDescription = "The kitchen tap has been dripping for a week.",
                SlotId = this.slot.Id,
                ServiceId = this.service.Id,
            };

            // A booking waiting for its deposit, as it is when the customer comes back from paying.
            public async Task<string> SeedPendingPaymentAsync(string address)
            {
                var booking = new Booking
                {
                    CustomerFirstName = "Ada",
                    CustomerLastName = "Lovelace",
                    Email = "ada@example.com",
                    PhoneNumber = "07700 900123",
                    Address = address,
                    ProblemDescription = "The kitchen tap has been dripping for a week.",
                    StatusId = this.DbContext.BookingStatuses.Single(s => s.Name == "Pending").Id,
                };
                this.DbContext.Bookings.Add(booking);
                this.DbContext.BookingServices.Add(new BookingService { BookingId = booking.Id, ServiceId = this.service.Id, PriceAtBooking = 90.00m, Quantity = 1 });
                this.slot.IsBooked = true;
                this.slot.BookingId = booking.Id;

                var payment = new Payment
                {
                    BookingId = booking.Id,
                    Amount = 50.00m,
                    Provider = "Stripe",
                    CheckoutSessionId = "cs_test_" + Guid.NewGuid().ToString("N"),
                    StatusId = this.DbContext.PaymentStatuses.Single(s => s.Name == "Pending").Id,
                };
                this.DbContext.Payments.Add(payment);
                await this.DbContext.SaveChangesAsync();
                return payment.CheckoutSessionId;
            }

            // Every send fails, or only the ones to one address.
            public void EmailsFail(string to = null)
            {
                this.EmailSender
                    .Setup(x => x.SendEmailAsync(
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.Is<string>(address => to == null || address == to),
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<IEnumerable<EmailAttachment>>(),
                        It.IsAny<string>()))
                    .ThrowsAsync(new InvalidOperationException("Brevo email send failed (500): try later"));
            }

            public List<SentEmail> CaptureEmails()
            {
                var sent = new List<SentEmail>();
                this.EmailSender
                    .Setup(x => x.SendEmailAsync(
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<IEnumerable<EmailAttachment>>(),
                        It.IsAny<string>()))
                    .Callback<string, string, string, string, string, IEnumerable<EmailAttachment>, string>(
                        (from, fromName, to, subject, body, attachments, replyTo) =>
                            sent.Add(new SentEmail { To = to, Body = body, ReplyTo = replyTo }))
                    .Returns(Task.CompletedTask);
                return sent;
            }
        }
    }
}
