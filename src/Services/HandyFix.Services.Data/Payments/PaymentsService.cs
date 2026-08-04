namespace HandyFix.Services.Data.Payments
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Data.Common.Repositories;
    using HandyFix.Data.Models;
    using HandyFix.Services.Mapping;
    using HandyFix.Services.Messaging;
    using HandyFix.Web.ViewModels.Payment;

    using Microsoft.AspNetCore.Hosting;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Hosting;

    using Stripe;
    using Stripe.Checkout;

    public class PaymentsService : IPaymentsService
    {
        private const string DefaultAdminNotificationEmail = "admin@handyfix.co.uk";
        private const string DefaultBookingsFromAddress = "bookings@handyfix.co.uk";
        private const string DefaultSystemFromAddress = "no-reply@handyfix.co.uk";

        private readonly IDeletableEntityRepository<Payment> paymentRepository;
        private readonly IDeletableEntityRepository<PaymentStatus> paymentStatusRepository;
        private readonly IDeletableEntityRepository<Booking> bookingRepository;
        private readonly IDeletableEntityRepository<BookingStatus> bookingStatusRepository;
        private readonly IEmailSender emailSender;
        private readonly IConfiguration configuration;
        private readonly IWebHostEnvironment environment;

        public PaymentsService(
            IDeletableEntityRepository<Payment> paymentRepository,
            IDeletableEntityRepository<PaymentStatus> paymentStatusRepository,
            IDeletableEntityRepository<Booking> bookingRepository,
            IDeletableEntityRepository<BookingStatus> bookingStatusRepository,
            IEmailSender emailSender,
            IConfiguration configuration,
            IWebHostEnvironment environment)
        {
            this.paymentRepository = paymentRepository;
            this.paymentStatusRepository = paymentStatusRepository;
            this.bookingRepository = bookingRepository;
            this.bookingStatusRepository = bookingStatusRepository;
            this.emailSender = emailSender;
            this.configuration = configuration;
            this.environment = environment;

            var secretKey = this.configuration["Stripe:SecretKey"];
            if (!string.IsNullOrWhiteSpace(secretKey))
            {
                StripeConfiguration.ApiKey = secretKey;
            }
        }

        public async Task<Guid> CreatePaymentRecordAsync(Guid bookingId, decimal amount, string provider, string checkoutSessionId)
        {
            PaymentStatus pendingStatus = await this.paymentStatusRepository.All().FirstOrDefaultAsync(x => x.Name == "Pending");
            if (pendingStatus == null)
            {
                throw new InvalidOperationException("Payment status 'Pending' is not seeded.");
            }

            // Supersede any earlier, still-pending payment attempt for this booking so a
            // customer retrying checkout doesn't pile up orphaned Pending rows pointing
            // at abandoned Stripe sessions.
            PaymentStatus cancelledStatus = await this.paymentStatusRepository.All().FirstOrDefaultAsync(x => x.Name == "Cancelled");
            if (cancelledStatus != null)
            {
                List<Payment> existingPendingPayments = await this.paymentRepository.All()
                    .Where(x => x.BookingId == bookingId && x.StatusId == pendingStatus.Id)
                    .ToListAsync();

                foreach (Payment existing in existingPendingPayments)
                {
                    existing.StatusId = cancelledStatus.Id;
                }
            }

            var payment = new Payment
            {
                BookingId = bookingId,
                Amount = amount,
                Provider = provider,
                CheckoutSessionId = checkoutSessionId,
                StatusId = pendingStatus.Id,
            };

            await this.paymentRepository.AddAsync(payment);
            await this.paymentRepository.SaveChangesAsync();

            return payment.Id;
        }

        public async Task ProcessPaymentSuccessAsync(string checkoutSessionId, string transactionId)
        {
            Payment payment = await this.paymentRepository.All()
                .Include(x => x.Booking).ThenInclude(b => b.BookingServices).ThenInclude(bs => bs.Service)
                .Include(x => x.Booking).ThenInclude(b => b.AvailabilitySlot)
                .FirstOrDefaultAsync(x => x.CheckoutSessionId == checkoutSessionId);

            if (payment == null)
            {
                return;
            }

            PaymentStatus paidStatus = await this.paymentStatusRepository.All().FirstOrDefaultAsync(x => x.Name == "DepositPaid");

            // The Stripe webhook and the browser's Success redirect can both call this
            // for the same session. Only the first call — the one that actually moves
            // the payment from Pending to DepositPaid — should trigger confirmation
            // emails; a repeat call is a harmless status re-confirmation.
            var alreadyProcessed = paidStatus != null && payment.StatusId == paidStatus.Id;

            // Set transaction ID
            payment.TransactionId = transactionId;

            // Change payment status to "DepositPaid"
            if (paidStatus != null)
            {
                payment.StatusId = paidStatus.Id;
            }

            // Update associated booking status to "Approved" (Confirmed deposit)
            Booking booking = payment.Booking;
            if (booking != null)
            {
                BookingStatus approvedStatus = await this.bookingStatusRepository.All().FirstOrDefaultAsync(x => x.Name == "Approved");
                if (approvedStatus != null)
                {
                    booking.StatusId = approvedStatus.Id;
                }
            }

            await this.paymentRepository.SaveChangesAsync();

            if (!alreadyProcessed && booking != null)
            {
                await this.SendBookingConfirmationEmailsAsync(booking, payment);
            }
        }

        private async Task SendBookingConfirmationEmailsAsync(Booking booking, Payment payment)
        {
            var serviceNames = booking.BookingServices != null
                ? string.Join(", ", booking.BookingServices.Select(x => x.Service?.Name).Where(name => !string.IsNullOrWhiteSpace(name)))
                : string.Empty;
            var scheduledTime = booking.AvailabilitySlot != null
                ? booking.AvailabilitySlot.StartTime.ToString("dd MMM yyyy 'at' HH:mm")
                : "To be confirmed";

            // Both addresses default to the not-yet-real @handyfix.co.uk placeholders and are
            // overridable via configuration -- Brevo (and any real email provider) rejects sends
            // from an unverified sender, so staging/local testing needs to point these at an
            // address that's actually verified in the Brevo account until the real domain lands.
            var bookingsFromAddress = this.configuration["Email:BookingsFromAddress"];
            if (string.IsNullOrWhiteSpace(bookingsFromAddress))
            {
                bookingsFromAddress = DefaultBookingsFromAddress;
            }

            var systemFromAddress = this.configuration["Email:SystemFromAddress"];
            if (string.IsNullOrWhiteSpace(systemFromAddress))
            {
                systemFromAddress = DefaultSystemFromAddress;
            }

            // No technician line here on purpose. A technician is assigned by an admin after the
            // deposit clears, so this email would always read "Not yet assigned" - which looks
            // unfinished to the customer. The technician detail belongs in the admin-approval
            // "CONFIRMED" email instead (BookingsService.UpdateStatusAsync).
            var clientSubject = "Your HandyFix Booking is Confirmed!";
            var clientBody = $@"
                <h3>Hi {booking.CustomerFirstName},</h3>
                <p>Great news — your deposit of £{payment.Amount:F2} has been received and your booking is now confirmed.</p>
                <ul>
                    <li><strong>Booking Reference:</strong> {booking.Id}</li>
                    <li><strong>Service(s):</strong> {serviceNames}</li>
                    <li><strong>Scheduled Time:</strong> {scheduledTime}</li>
                    <li><strong>Address:</strong> {booking.Address}</li>
                </ul>
                <p>We'll confirm your assigned technician shortly.</p>
                <p>We look forward to helping you. Thank you for choosing HandyFix!</p>";

            await this.emailSender.SendEmailAsync(
                bookingsFromAddress,
                "HandyFix Bookings",
                booking.Email,
                clientSubject,
                clientBody);

            var adminEmail = this.configuration["Admin:NotificationEmail"];
            if (string.IsNullOrWhiteSpace(adminEmail))
            {
                adminEmail = DefaultAdminNotificationEmail;
            }

            var adminSubject = $"New Confirmed Booking - {booking.CustomerFirstName} {booking.CustomerLastName}";
            var adminBody = $@"
                <h3>A booking deposit has just been paid.</h3>
                <ul>
                    <li><strong>Booking Reference:</strong> {booking.Id}</li>
                    <li><strong>Customer:</strong> {booking.CustomerFirstName} {booking.CustomerLastName} ({booking.Email}, {booking.PhoneNumber})</li>
                    <li><strong>Service(s):</strong> {serviceNames}</li>
                    <li><strong>Scheduled Time:</strong> {scheduledTime}</li>
                    <li><strong>Address:</strong> {booking.Address}</li>
                    <li><strong>Deposit Paid:</strong> £{payment.Amount:F2}</li>
                </ul>
                <p>Please review and assign a technician if one isn't already set.</p>";

            await this.emailSender.SendEmailAsync(
                systemFromAddress,
                "HandyFix System",
                adminEmail,
                adminSubject,
                adminBody);
        }

        public async Task CancelPaymentAsync(string checkoutSessionId)
        {
            Payment payment = await this.paymentRepository.All()
                .FirstOrDefaultAsync(x => x.CheckoutSessionId == checkoutSessionId);
            if (payment == null)
            {
                return;
            }

            PaymentStatus pendingStatus = await this.paymentStatusRepository.All().FirstOrDefaultAsync(x => x.Name == "Pending");
            PaymentStatus cancelledStatus = await this.paymentStatusRepository.All().FirstOrDefaultAsync(x => x.Name == "Cancelled");
            if (cancelledStatus == null)
            {
                return;
            }

            // Only a still-pending payment can expire; never overwrite a payment that
            // already succeeded (e.g. the success webhook raced ahead of this one).
            if (pendingStatus != null && payment.StatusId != pendingStatus.Id)
            {
                return;
            }

            payment.StatusId = cancelledStatus.Id;
            await this.paymentRepository.SaveChangesAsync();
        }

        public async Task CancelPendingPaymentsForBookingsAsync(IEnumerable<Guid> bookingIds)
        {
            PaymentStatus pendingStatus = await this.paymentStatusRepository.All().FirstOrDefaultAsync(x => x.Name == "Pending");
            PaymentStatus cancelledStatus = await this.paymentStatusRepository.All().FirstOrDefaultAsync(x => x.Name == "Cancelled");
            if (pendingStatus == null || cancelledStatus == null)
            {
                return;
            }

            List<Guid> idList = bookingIds?.ToList() ?? new List<Guid>();
            if (idList.Count == 0)
            {
                return;
            }

            List<Payment> pendingPayments = await this.paymentRepository.All()
                .Where(x => idList.Contains(x.BookingId) && x.StatusId == pendingStatus.Id)
                .ToListAsync();

            if (pendingPayments.Count == 0)
            {
                return;
            }

            foreach (Payment payment in pendingPayments)
            {
                payment.StatusId = cancelledStatus.Id;
            }

            await this.paymentRepository.SaveChangesAsync();
        }

        public async Task<IEnumerable<T>> GetPaymentsForBookingAsync<T>(Guid bookingId)
        {
            return await this.paymentRepository.All()
                .Where(x => x.BookingId == bookingId)
                .OrderByDescending(x => x.CreatedOn)
                .To<T>()
                .ToListAsync();
        }

        public async Task<IEnumerable<T>> GetAllPaymentsAsync<T>()
        {
            return await this.paymentRepository.All()
                .OrderByDescending(x => x.CreatedOn)
                .To<T>()
                .ToListAsync();
        }

        public async Task<decimal> GetTotalRevenueAsync()
        {
            return await this.paymentRepository.All()
                .Where(x => x.Status.Name == "DepositPaid" || x.Status.Name == "Completed")
                .SumAsync(x => (decimal?)x.Amount) ?? 0.00m;
        }

        public async Task<PaymentCheckoutResult> CreateCheckoutSessionAsync(Guid bookingId, decimal depositAmount, string successUrl, string cancelUrl)
        {
            var secretKey = this.configuration["Stripe:SecretKey"];
            var keyMissing = string.IsNullOrWhiteSpace(secretKey);

            // Sandbox Mode bypasses Stripe entirely so the booking flow can still be exercised
            // without a real account. Always allowed in Development. Outside Development it
            // requires an explicit opt-in (Stripe:AllowSandboxOutsideDevelopment) so a staging
            // environment can demo the flow before a real Stripe account exists, while production
            // stays protected by default -- that flag must never be set there.
            var sandboxAllowed = this.environment.IsDevelopment()
                || this.configuration.GetValue<bool>("Stripe:AllowSandboxOutsideDevelopment");

            if (keyMissing && sandboxAllowed)
            {
                var mockSessionId = $"mock_session_{Guid.NewGuid()}";
                await this.CreatePaymentRecordAsync(bookingId, depositAmount, "Stripe-Mock", mockSessionId);
                return new PaymentCheckoutResult { IsMock = true, SessionId = mockSessionId };
            }

            if (keyMissing)
            {
                // Never silently fake a payment or attempt a doomed Stripe call outside
                // development: fail loudly so a missing production secret gets noticed.
                throw new InvalidOperationException("Stripe is not configured for this environment. Set Stripe:SecretKey before accepting real payments.");
            }

            var options = new SessionCreateOptions
            {
                PaymentMethodTypes = new List<string> { "card" },
                LineItems = new List<SessionLineItemOptions>
                {
                    new SessionLineItemOptions
                    {
                        PriceData = new SessionLineItemPriceDataOptions
                        {
                            UnitAmount = (long)(depositAmount * 100), // convert to cents
                            Currency = "gbp",
                            ProductData = new SessionLineItemPriceDataProductDataOptions
                            {
                                Name = $"HandyFix Booking Deposit (Ref: {bookingId})",
                                Description = "Deposit to secure your service booking for South London.",
                            },
                        },
                        Quantity = 1,
                    },
                },
                Mode = "payment",
                SuccessUrl = successUrl,
                CancelUrl = cancelUrl,
            };

            var sessionService = new SessionService();
            Session session = await sessionService.CreateAsync(options);

            await this.CreatePaymentRecordAsync(bookingId, depositAmount, "Stripe", session.Id);

            return new PaymentCheckoutResult { IsMock = false, SessionId = session.Id, RedirectUrl = session.Url };
        }

        public async Task HandleWebhookEventAsync(string json, string signature)
        {
            var webhookSecret = this.configuration["Stripe:WebhookSecret"];
            Event stripeEvent = EventUtility.ConstructEvent(json, signature, webhookSecret);

            if (stripeEvent.Type == Events.CheckoutSessionCompleted)
            {
                var session = stripeEvent.Data.Object as Session;
                if (session != null)
                {
                    await this.ProcessPaymentSuccessAsync(session.Id, session.PaymentIntentId);
                }
            }
            else if (stripeEvent.Type == Events.CheckoutSessionExpired)
            {
                var session = stripeEvent.Data.Object as Session;
                if (session != null)
                {
                    await this.CancelPaymentAsync(session.Id);
                }
            }
        }
    }
}
