namespace HandyFix.Services.Data.Payments
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Common;
    using HandyFix.Data.Common.Repositories;
    using HandyFix.Data.Models;
    using HandyFix.Services.Data.Common;
    using HandyFix.Services.Mapping;
    using HandyFix.Services.Messaging;
    using HandyFix.Web.ViewModels.Payment;

    using Microsoft.AspNetCore.Hosting;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;

    using Stripe;
    using Stripe.Checkout;

    public class PaymentsService : IPaymentsService
    {
        private readonly IDeletableEntityRepository<Payment> paymentRepository;
        private readonly IDeletableEntityRepository<PaymentStatus> paymentStatusRepository;
        private readonly IDeletableEntityRepository<Booking> bookingRepository;
        private readonly IDeletableEntityRepository<BookingStatus> bookingStatusRepository;
        private readonly IEmailSender emailSender;
        private readonly IConfiguration configuration;
        private readonly IWebHostEnvironment environment;
        private readonly ILogger<PaymentsService> logger;

        public PaymentsService(
            IDeletableEntityRepository<Payment> paymentRepository,
            IDeletableEntityRepository<PaymentStatus> paymentStatusRepository,
            IDeletableEntityRepository<Booking> bookingRepository,
            IDeletableEntityRepository<BookingStatus> bookingStatusRepository,
            IEmailSender emailSender,
            IConfiguration configuration,
            IWebHostEnvironment environment,
            ILogger<PaymentsService> logger)
        {
            this.paymentRepository = paymentRepository;
            this.paymentStatusRepository = paymentStatusRepository;
            this.bookingRepository = bookingRepository;
            this.bookingStatusRepository = bookingStatusRepository;
            this.emailSender = emailSender;
            this.configuration = configuration;
            this.environment = environment;
            this.logger = logger;

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

            // The deposit is paid and the booking approved by now, so neither send is allowed to
            // throw: a failure here used to end the customer's return from Stripe on an error page,
            // straight after paying. It is logged, and the other email still goes
            // (PROJECT_STATE Section 3cb). What the customer typed is encoded before it goes into
            // either email's HTML.
            var customerName = EmailText.Encode($"{booking.CustomerFirstName} {booking.CustomerLastName}");
            var address = EmailText.Encode(booking.Address);
            serviceNames = EmailText.Encode(serviceNames);

            // No technician line here on purpose. A technician is picked by an admin after the
            // deposit clears, so this email would always read "Not yet assigned" - which looks
            // unfinished to the customer. The technician's name and number go out in an email of
            // their own, when one is picked (BookingsService.AssignTechnicianAsync).
            //
            // This is the first email a customer gets: nothing is sent before the deposit is paid
            // (BookingsService.CreateBookingAsync).
            var reference = BookingReference.Short(booking.Id);
            var clientSubject = "Your Plumbing Handyman Surrey Booking is Confirmed!";
            var clientBody = $@"
                <h3>Hi {EmailText.Encode(booking.CustomerFirstName)},</h3>
                <p>Great news! Your deposit of £{payment.Amount:F2} has been received and your booking is now confirmed.</p>
                <ul>
                    <li><strong>Booking Reference:</strong> {reference}</li>
                    <li><strong>Service(s):</strong> {serviceNames}</li>
                    <li><strong>Scheduled Time:</strong> {scheduledTime}</li>
                    <li><strong>Address:</strong> {address}</li>
                </ul>
                <p>We'll email you your technician's name and phone number as soon as one is assigned.</p>
                <p>We look forward to helping you. Thank you for choosing Plumbing Handyman Surrey!</p>";

            await this.emailSender.TrySendEmailAsync(
                this.logger,
                "deposit paid, to the customer",
                EmailSettings.BookingsFromAddress(this.configuration),
                EmailSettings.CustomerFromName,
                booking.Email,
                clientSubject,
                clientBody);

            var adminSubject = $"New Confirmed Booking - {booking.CustomerFirstName} {booking.CustomerLastName}";
            var adminBody = $@"
                <h3>A booking deposit has just been paid.</h3>
                <ul>
                    <li><strong>Booking Reference:</strong> {reference}</li>
                    <li><strong>Customer:</strong> {customerName} ({EmailText.Encode(booking.Email)}, {EmailText.Encode(booking.PhoneNumber)})</li>
                    <li><strong>Service(s):</strong> {serviceNames}</li>
                    <li><strong>Scheduled Time:</strong> {scheduledTime}</li>
                    <li><strong>Address:</strong> {address}</li>
                    <li><strong>Deposit Paid:</strong> £{payment.Amount:F2}</li>
                </ul>
                <p>Next: pick a technician on the booking's page in the admin panel. The customer is emailed the name and number when you do.</p>";

            // Reply-To is the customer, so pressing Reply on this notice answers them.
            await this.emailSender.TrySendEmailAsync(
                this.logger,
                "deposit paid, notice to the company",
                EmailSettings.SystemFromAddress(this.configuration),
                EmailSettings.WebsiteFromName,
                EmailSettings.AdminNotificationAddress(this.configuration),
                adminSubject,
                adminBody,
                replyTo: booking.Email);
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
                                Name = $"Plumbing Handyman Surrey Booking Deposit (Ref: {BookingReference.Short(bookingId)})",
                                Description = "Deposit to secure your service booking.",
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
