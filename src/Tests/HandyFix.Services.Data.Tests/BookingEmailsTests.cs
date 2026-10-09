namespace HandyFix.Services.Data.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Common;
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

    // The booking emails each follow something already saved: the deposit, or a technician being
    // picked. A send that fails must not undo or hide that, and what a customer typed must reach
    // an email as text (PROJECT_STATE.md Section 3cb). Which emails go out, and when, is
    // PROJECT_STATE.md Section 3ce: nothing before the deposit, and the technician's name when an
    // admin picks one, which is also where the rules for the admin's three buttons are tested.
    public class BookingEmailsTests
    {
        // A booking that is never paid is abandoned after about fifteen minutes. The email this
        // used to send, "we have received your booking", promised a visit that could be gone
        // before it was read.
        [Fact]
        public async Task CreateBookingAsyncShouldSendNoEmail()
        {
            using var world = new World();
            List<SentEmail> sent = world.CaptureEmails();

            Booking booking = await world.Bookings.CreateBookingAsync(world.BookingFor("12 Main Rd, Sutton"), new List<string>());

            Assert.NotNull(booking);
            Assert.Single(world.DbContext.Bookings);
            Assert.True(world.DbContext.AvailabilitySlots.Single().IsBooked);
            Assert.Empty(sent);
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

        // The emails used to show all 36 characters of the booking's id while the admin pages
        // showed the first eight, so a reference read out over the phone matched nothing on the
        // admin's screen.
        [Fact]
        public async Task TheDepositEmailsShouldCarryTheShortReferenceTheAdminPagesShow()
        {
            using var world = new World();
            var checkoutSessionId = await world.SeedPendingPaymentAsync("12 Main Rd, Sutton");
            Guid bookingId = world.DbContext.Bookings.Single().Id;
            List<SentEmail> sent = world.CaptureEmails();

            await world.Payments.ProcessPaymentSuccessAsync(checkoutSessionId, "txn_1");

            Assert.Equal(2, sent.Count);
            Assert.All(sent, email => Assert.Contains("<strong>Booking Reference:</strong> " + BookingReference.Short(bookingId) + "</li>", email.Body));
            Assert.All(sent, email => Assert.DoesNotContain(bookingId.ToString(), email.Body));
        }

        // A technician may be on the roster under a first name alone, and a number is written
        // with spaces (PROJECT_STATE.md Section 3cd). The name must not drag a space behind it,
        // and the link's address holds the number without the spaces the text keeps.
        [Theory]
        [InlineData(null, "<strong>Zapryan</strong>")]
        [InlineData("Stone", "<strong>Zapryan Stone</strong>")]
        public async Task PickingATechnicianShouldEmailTheCustomerTheNameAndLinkTheNumber(string lastName, string expectedName)
        {
            using var world = new World();
            Booking booking = await world.SeedPaidBookingAsync();
            Technician technician = world.AddTechnician("Zapryan", lastName);
            List<SentEmail> sent = world.CaptureEmails();

            TechnicianAssignmentResult result = await world.Bookings.AssignTechnicianAsync(booking.Id, technician.Id);

            Assert.Equal(TechnicianAssignmentOutcome.AssignedAndCustomerEmailed, result.Outcome);
            Assert.Equal(NameFormat.Full("Zapryan", lastName), result.TechnicianName);
            Assert.Equal(technician.Id, world.DbContext.Bookings.Single().TechnicianId);

            SentEmail email = Assert.Single(sent);
            Assert.Equal("ada@example.com", email.To);
            Assert.Equal("Your technician for your Plumbing Handyman Surrey booking", email.Subject);
            Assert.Contains(expectedName, email.Body);
            Assert.Contains("<a href=\"tel:02039515915\">020 3951 5915</a>", email.Body);
            Assert.Contains(BookingReference.Short(booking.Id), email.Body);
            Assert.DoesNotContain(booking.Id.ToString(), email.Body);
            Assert.Contains("Leak Fix", email.Body);
            Assert.Contains(world.SlotStart.ToString("dd MMM yyyy 'at' HH:mm"), email.Body);
        }

        [Fact]
        public async Task TheTechnicianEmailShouldCarryWhatTheCustomerTypedAsText()
        {
            using var world = new World();
            Booking booking = await world.SeedPaidBookingAsync("<b>12 Main Rd</b> & Sons, Sutton");
            Technician technician = world.AddTechnician("Zapryan");
            List<SentEmail> sent = world.CaptureEmails();

            await world.Bookings.AssignTechnicianAsync(booking.Id, technician.Id);

            SentEmail email = Assert.Single(sent);
            Assert.Contains("&lt;b&gt;12 Main Rd&lt;/b&gt; &amp; Sons, Sutton", email.Body);
            Assert.DoesNotContain("<b>12 Main Rd</b>", email.Body);
        }

        // The technician is saved before the email goes. A failed send must not turn the admin's
        // page into an error page, and must not be reported as sent: the admin is the only one
        // who can still tell the customer.
        [Fact]
        public async Task PickingATechnicianShouldKeepTheTechnicianAndSayTheEmailWasNotSentWhenItCannotBeSent()
        {
            using var world = new World();
            Booking booking = await world.SeedPaidBookingAsync();
            Technician technician = world.AddTechnician("Zapryan");
            world.EmailsFail();

            TechnicianAssignmentResult result = await world.Bookings.AssignTechnicianAsync(booking.Id, technician.Id);

            Assert.Equal(TechnicianAssignmentOutcome.AssignedButEmailNotSent, result.Outcome);
            Assert.Equal("Zapryan", result.TechnicianName);
            Assert.Equal(technician.Id, world.DbContext.Bookings.Single().TechnicianId);
        }

        // An unpaid booking is dropped after about fifteen minutes, so until the deposit is in
        // there is no job to send anyone to. The page shows no picker for it; this is the check
        // a page left open cannot get round.
        [Fact]
        public async Task PickingATechnicianShouldBeRefusedUntilTheDepositIsPaid()
        {
            using var world = new World();
            await world.SeedPendingPaymentAsync("12 Main Rd, Sutton");
            Booking booking = world.DbContext.Bookings.Single();
            Technician technician = world.AddTechnician("Zapryan");
            List<SentEmail> sent = world.CaptureEmails();

            TechnicianAssignmentResult result = await world.Bookings.AssignTechnicianAsync(booking.Id, technician.Id);

            Assert.Equal(TechnicianAssignmentOutcome.NotAllowed, result.Outcome);
            Assert.Null(world.DbContext.Bookings.Single().TechnicianId);
            Assert.Empty(sent);
        }

        // A customer whose booking was called off, or whose job is finished, must not be emailed
        // "your technician is" by a page that was opened before that.
        [Theory]
        [InlineData("Cancelled")]
        [InlineData("Completed")]
        [InlineData("Abandoned")]
        public async Task PickingATechnicianShouldBeRefusedOnABookingThatIsClosed(string status)
        {
            using var world = new World();
            Booking booking = await world.SeedPaidBookingAsync();
            world.SetStatus(booking, status);
            Technician technician = world.AddTechnician("Zapryan");
            List<SentEmail> sent = world.CaptureEmails();

            TechnicianAssignmentResult result = await world.Bookings.AssignTechnicianAsync(booking.Id, technician.Id);

            Assert.Equal(TechnicianAssignmentOutcome.NotAllowed, result.Outcome);
            Assert.Null(world.DbContext.Bookings.Single().TechnicianId);
            Assert.Empty(sent);
        }

        // "Update Assignment" pressed on the form as it stands. The customer was told once.
        [Fact]
        public async Task PickingTheSameTechnicianAgainShouldChangeNothingAndSendNoSecondEmail()
        {
            using var world = new World();
            Booking booking = await world.SeedPaidBookingAsync();
            Technician technician = world.AddTechnician("Zapryan");
            List<SentEmail> sent = world.CaptureEmails();

            await world.Bookings.AssignTechnicianAsync(booking.Id, technician.Id);
            TechnicianAssignmentResult again = await world.Bookings.AssignTechnicianAsync(booking.Id, technician.Id);

            Assert.Equal(TechnicianAssignmentOutcome.Unchanged, again.Outcome);
            Assert.Equal("Zapryan", again.TechnicianName);
            Assert.Single(sent);
        }

        // Somebody else is coming, so the customer is told who.
        [Fact]
        public async Task PickingAnotherTechnicianShouldEmailTheCustomerTheNewName()
        {
            using var world = new World();
            Booking booking = await world.SeedPaidBookingAsync();
            Technician first = world.AddTechnician("Zapryan");
            Technician second = world.AddTechnician("Sam", "Reed", "07700 900456");
            List<SentEmail> sent = world.CaptureEmails();

            await world.Bookings.AssignTechnicianAsync(booking.Id, first.Id);
            TechnicianAssignmentResult result = await world.Bookings.AssignTechnicianAsync(booking.Id, second.Id);

            Assert.Equal(TechnicianAssignmentOutcome.AssignedAndCustomerEmailed, result.Outcome);
            Assert.Equal("Sam Reed", result.TechnicianName);
            Assert.Equal(2, sent.Count);
            Assert.Contains("<strong>Sam Reed</strong>", sent[1].Body);
            Assert.Contains("<a href=\"tel:07700900456\">07700 900456</a>", sent[1].Body);
            Assert.DoesNotContain("Zapryan", sent[1].Body);
        }

        [Fact]
        public async Task TakingTheTechnicianOffShouldSendNoEmail()
        {
            using var world = new World();
            Booking booking = await world.SeedPaidBookingAsync();
            Technician technician = world.AddTechnician("Zapryan");
            await world.Bookings.AssignTechnicianAsync(booking.Id, technician.Id);
            List<SentEmail> sent = world.CaptureEmails();

            TechnicianAssignmentResult result = await world.Bookings.AssignTechnicianAsync(booking.Id, null);

            Assert.Equal(TechnicianAssignmentOutcome.Cleared, result.Outcome);
            Assert.Null(result.TechnicianName);
            Assert.Null(world.DbContext.Bookings.Single().TechnicianId);
            Assert.Empty(sent);
        }

        // An id that is no technician's (Guid.Empty is the easy one to post) used to reach the
        // database as a foreign key that points at nothing (PROJECT_STATE.md Section 3r).
        [Fact]
        public async Task PickingATechnicianWhoIsNotOnTheRosterShouldChangeNothing()
        {
            using var world = new World();
            Booking booking = await world.SeedPaidBookingAsync();
            List<SentEmail> sent = world.CaptureEmails();

            TechnicianAssignmentResult result = await world.Bookings.AssignTechnicianAsync(booking.Id, Guid.Empty);

            Assert.Equal(TechnicianAssignmentOutcome.TechnicianNotFound, result.Outcome);
            Assert.Null(world.DbContext.Bookings.Single().TechnicianId);
            Assert.Empty(sent);
        }

        [Fact]
        public async Task PickingATechnicianForABookingThatDoesNotExistShouldSaySo()
        {
            using var world = new World();

            TechnicianAssignmentResult result = await world.Bookings.AssignTechnicianAsync(Guid.NewGuid(), null);

            Assert.Equal(TechnicianAssignmentOutcome.BookingNotFound, result.Outcome);
        }

        [Fact]
        public async Task CompleteBookingAsyncShouldCompleteABookingWhoseDepositIsPaid()
        {
            using var world = new World();
            Booking booking = await world.SeedPaidBookingAsync();

            var completed = await world.Bookings.CompleteBookingAsync(booking.Id);

            Assert.True(completed);
            Assert.Equal("Completed", world.StatusOf(booking));
        }

        // "Complete Service" used to be offered on a booking nobody had paid for.
        [Fact]
        public async Task CompleteBookingAsyncShouldRefuseABookingStillWaitingForItsDeposit()
        {
            using var world = new World();
            await world.SeedPendingPaymentAsync("12 Main Rd, Sutton");
            Booking booking = world.DbContext.Bookings.Single();

            var completed = await world.Bookings.CompleteBookingAsync(booking.Id);

            Assert.False(completed);
            Assert.Equal("Pending", world.StatusOf(booking));
        }

        [Theory]
        [InlineData("Cancelled")]
        [InlineData("Abandoned")]
        [InlineData("Completed")]
        public async Task CompleteBookingAsyncShouldRefuseABookingThatIsClosed(string status)
        {
            using var world = new World();
            Booking booking = await world.SeedPaidBookingAsync();
            world.SetStatus(booking, status);

            var completed = await world.Bookings.CompleteBookingAsync(booking.Id);

            Assert.False(completed);
            Assert.Equal(status, world.StatusOf(booking));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task CancelBookingAsyncShouldCancelAnOpenBookingAndFreeItsSlot(bool depositPaid)
        {
            using var world = new World();
            if (depositPaid)
            {
                await world.SeedPaidBookingAsync();
            }
            else
            {
                await world.SeedPendingPaymentAsync("12 Main Rd, Sutton");
            }

            Booking booking = world.DbContext.Bookings.Single();

            var cancelled = await world.Bookings.CancelBookingAsync(booking.Id);

            Assert.True(cancelled);
            Assert.Equal("Cancelled", world.StatusOf(booking));
            Assert.False(world.DbContext.AvailabilitySlots.Single().IsBooked);
        }

        // "Cancel Booking" used to be offered on a completed booking, and on an abandoned one.
        // Cancelling a finished job would have relabelled it and put its hour back on sale.
        [Theory]
        [InlineData("Completed")]
        [InlineData("Cancelled")]
        [InlineData("Abandoned")]
        public async Task CancelBookingAsyncShouldRefuseABookingThatIsClosed(string status)
        {
            using var world = new World();
            Booking booking = await world.SeedPaidBookingAsync();
            world.SetStatus(booking, status);

            var cancelled = await world.Bookings.CancelBookingAsync(booking.Id);

            Assert.False(cancelled);
            Assert.Equal(status, world.StatusOf(booking));
            Assert.True(world.DbContext.AvailabilitySlots.Single().IsBooked);
        }

        private sealed class SentEmail
        {
            public string To { get; set; }

            public string Subject { get; set; }

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
                    new BookingStatus { Name = "Approved" },
                    new BookingStatus { Name = "Completed" },
                    new BookingStatus { Name = "Cancelled" },
                    new BookingStatus { Name = "Abandoned" });
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

            public DateTime SlotStart => this.slot.StartTime;

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

            // A booking with its deposit paid, as it stands when the admin opens it: made the
            // way the site makes one, by the payment coming in.
            public async Task<Booking> SeedPaidBookingAsync(string address = "12 Main Rd, Sutton")
            {
                var checkoutSessionId = await this.SeedPendingPaymentAsync(address);
                await this.Payments.ProcessPaymentSuccessAsync(checkoutSessionId, "txn_1");
                return this.DbContext.Bookings.Single();
            }

            public Technician AddTechnician(string firstName, string lastName = null, string phoneNumber = "020 3951 5915")
            {
                var technician = new Technician { FirstName = firstName, LastName = lastName, PhoneNumber = phoneNumber };
                this.DbContext.Technicians.Add(technician);
                this.DbContext.SaveChanges();
                return technician;
            }

            public void SetStatus(Booking booking, string status)
            {
                booking.StatusId = this.DbContext.BookingStatuses.Single(s => s.Name == status).Id;
                this.DbContext.SaveChanges();
            }

            public string StatusOf(Booking booking) =>
                this.DbContext.Bookings.Include(b => b.Status).Single(b => b.Id == booking.Id).Status.Name;

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
                            sent.Add(new SentEmail { To = to, Subject = subject, Body = body, ReplyTo = replyTo }))
                    .Returns(Task.CompletedTask);
                return sent;
            }
        }
    }
}
