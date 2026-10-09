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

    // A database with the statuses, one service and one free slot, and the booking, payment and
    // availability services wired to it with a mocked email sender. What the tests of a job's
    // emails, buttons, money and history stand on (PROJECT_STATE.md Section 3ce).
    internal sealed class BookingWorld : IDisposable
    {
        private readonly Service service;
        private readonly AvailabilitySlot slot;

        // With a Stripe key the payments service goes to Stripe (the stand-in, here) as the live
        // site does; without one, and in Development, it uses the pretend payment.
        public BookingWorld(string stripeSecretKey = null, string environmentName = null)
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
                new PaymentStatus { Name = "DepositPaid" },
                new PaymentStatus { Name = "Completed" },
                new PaymentStatus { Name = "Cancelled" },
                new PaymentStatus { Name = "Refunded" });

            var category = new ServiceCategory { Name = "Plumbing", Description = "Plumbing", Slug = "plumbing" };
            this.DbContext.ServiceCategories.Add(category);

            this.service = new Service { Name = "Leak Fix", Description = "Repair a leak", Slug = "leak-fix", BasePrice = 90.00m, CategoryId = category.Id };
            this.DbContext.Services.Add(this.service);

            this.slot = new AvailabilitySlot { StartTime = DateTime.Today.AddDays(1).AddHours(9), EndTime = DateTime.Today.AddDays(1).AddHours(10) };
            this.DbContext.AvailabilitySlots.Add(this.slot);
            this.DbContext.SaveChanges();

            var settings = new Dictionary<string, string>();
            if (stripeSecretKey != null)
            {
                settings["Stripe:SecretKey"] = stripeSecretKey;
                settings["Stripe:WebhookSecret"] = "whsec_test";
            }

            IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
            var environment = new Mock<IWebHostEnvironment>();
            environment.SetupGet(x => x.EnvironmentName).Returns(environmentName);

            var bookingRepo = new EfDeletableEntityRepository<Booking>(this.DbContext);
            var bookingStatusRepo = new EfDeletableEntityRepository<BookingStatus>(this.DbContext);
            var slotRepo = new EfDeletableEntityRepository<AvailabilitySlot>(this.DbContext);

            this.Availability = new AvailabilityService(slotRepo);

            this.Payments = new PaymentsService(
                new EfDeletableEntityRepository<Payment>(this.DbContext),
                new EfDeletableEntityRepository<PaymentStatus>(this.DbContext),
                bookingRepo,
                bookingStatusRepo,
                this.EmailSender.Object,
                configuration,
                environment.Object,
                NullLogger<PaymentsService>.Instance,
                this.Stripe.Object);

            this.Bookings = new BookingsService(
                bookingRepo,
                new EfDeletableEntityRepository<Service>(this.DbContext),
                slotRepo,
                bookingStatusRepo,
                new EfDeletableEntityRepository<BookingImage>(this.DbContext),
                new EfDeletableEntityRepository<Technician>(this.DbContext),
                new EfDeletableEntityRepository<BookingService>(this.DbContext),
                this.Availability,
                this.Payments,
                new DbQueryRunner(this.DbContext),
                this.EmailSender.Object,
                configuration,
                NullLogger<BookingsService>.Instance);
        }

        public ApplicationDbContext DbContext { get; }

        public Mock<IEmailSender> EmailSender { get; } = new Mock<IEmailSender>();

        // Stands in for Stripe. Left alone it answers nothing, which the payments service reads
        // as a payment page that is not paid and no longer open.
        public Mock<IStripeGateway> Stripe { get; } = new Mock<IStripeGateway>();

        public BookingsService Bookings { get; }

        public PaymentsService Payments { get; }

        public AvailabilityService Availability { get; }

        public Guid ServiceId => this.service.Id;

        public Guid SlotId => this.slot.Id;

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
                Source = BookingSource.Website,
                ScheduledStart = this.slot.StartTime,
                ScheduledEnd = this.slot.EndTime,
                TotalAmount = 90.00m,
                DepositAmount = 50.00m,
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

        // A job the admin writes in with as little as the form allows: a first name, a phone
        // number, a day, a time and where it came from.
        public JobInputModel WrittenInJob(DateTime? start = null, BookingSource source = BookingSource.Phone)
        {
            DateTime when = start ?? DateTime.Today.AddDays(2).AddHours(14);
            return new JobInputModel
            {
                CustomerFirstName = "Grace",
                PhoneNumber = "07700 900456",
                Date = when.Date,
                Time = when.TimeOfDay,
                Source = source,
            };
        }

        public async Task<Booking> SeedWrittenInJobAsync(DateTime? start = null)
        {
            Guid id = await this.Bookings.CreateWrittenInJobAsync(this.WrittenInJob(start));
            return this.DbContext.Bookings.Single(x => x.Id == id);
        }

        // The form that puts a job's details right, filled in with what the job holds now: what
        // the admin's page sends back when nothing on it is touched.
        public JobEditInputModel EditFormOf(Booking booking)
        {
            Booking job = this.DbContext.Bookings.Include(x => x.BookingServices).Single(x => x.Id == booking.Id);

            return new JobEditInputModel
            {
                Id = job.Id,
                CustomerFirstName = job.CustomerFirstName,
                CustomerLastName = job.CustomerLastName,
                PhoneNumber = job.PhoneNumber,
                Email = job.Email,
                Address = job.Address,
                ServiceId = job.BookingServices.Where(x => !x.IsDeleted).Select(x => (Guid?)x.ServiceId).FirstOrDefault(),
                ProblemDescription = job.ProblemDescription,
                Source = job.Source == BookingSource.Website ? null : job.Source,
            };
        }

        // Another service on the list, for a job whose service is changed.
        public Service AddService(string name, decimal price)
        {
            var added = new Service
            {
                Name = name,
                Description = name,
                Slug = name.ToLowerInvariant().Replace(' ', '-'),
                BasePrice = price,
                CategoryId = this.service.CategoryId,
            };
            this.DbContext.Services.Add(added);
            this.DbContext.SaveChanges();
            return added;
        }

        public Technician AddTechnician(string firstName, string lastName = null, string phoneNumber = "020 3951 5915")
        {
            var technician = new Technician { FirstName = firstName, LastName = lastName, PhoneNumber = phoneNumber };
            this.DbContext.Technicians.Add(technician);
            this.DbContext.SaveChanges();
            return technician;
        }

        // Another hour in the calendar, free unless said otherwise.
        public AvailabilitySlot AddSlot(DateTime start, bool blocked = false, Guid? heldBy = null)
        {
            var added = new AvailabilitySlot
            {
                StartTime = start,
                EndTime = start.AddHours(1),
                IsBlocked = blocked,
                IsBooked = heldBy.HasValue,
                BookingId = heldBy,
            };
            this.DbContext.AvailabilitySlots.Add(added);
            this.DbContext.SaveChanges();
            return added;
        }

        public AvailabilitySlot Slot(Guid id) => this.DbContext.AvailabilitySlots.Single(x => x.Id == id);

        public void SetStatus(Booking booking, string status)
        {
            booking.StatusId = this.DbContext.BookingStatuses.Single(s => s.Name == status).Id;
            this.DbContext.SaveChanges();
        }

        public string StatusOf(Booking booking) =>
            this.DbContext.Bookings.Include(b => b.Status).Single(b => b.Id == booking.Id).Status.Name;

        // The job's history lines as they were written, oldest first.
        public List<string> HistoryOf(Booking booking) =>
            this.DbContext.BookingHistoryEntries
                .Where(x => x.BookingId == booking.Id)
                .OrderBy(x => x.CreatedOn)
                .Select(x => x.Text)
                .ToList();

        // The statuses of the job's payments that are still on it.
        public List<string> PaymentStatusesOf(Booking booking) =>
            this.DbContext.Payments
                .Where(x => x.BookingId == booking.Id)
                .Select(x => x.Status.Name)
                .ToList();

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
