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
        private const string StripeProvider = "Stripe";
        private const string MockProvider = "Stripe-Mock";

        // On both notices to the company: their Reply-To is the customer's address.
        private const string ReplyTip = "<em>Tip: Pressing &quot;Reply&quot; in your email inbox replies directly to the customer.</em>";

        // Stripe keeps a payment page open for a day unless told otherwise, and will not take
        // less than thirty minutes. The site closes the page itself when it drops the booking
        // (CloseCheckoutsAsync); this is what is left if that call never gets through.
        private static readonly TimeSpan CheckoutLifetime = TimeSpan.FromMinutes(31);

        private readonly IDeletableEntityRepository<Payment> paymentRepository;
        private readonly IDeletableEntityRepository<PaymentStatus> paymentStatusRepository;
        private readonly IDeletableEntityRepository<Booking> bookingRepository;
        private readonly IDeletableEntityRepository<BookingStatus> bookingStatusRepository;
        private readonly IEmailSender emailSender;
        private readonly IConfiguration configuration;
        private readonly IWebHostEnvironment environment;
        private readonly ILogger<PaymentsService> logger;
        private readonly IStripeGateway stripeGateway;

        public PaymentsService(
            IDeletableEntityRepository<Payment> paymentRepository,
            IDeletableEntityRepository<PaymentStatus> paymentStatusRepository,
            IDeletableEntityRepository<Booking> bookingRepository,
            IDeletableEntityRepository<BookingStatus> bookingStatusRepository,
            IEmailSender emailSender,
            IConfiguration configuration,
            IWebHostEnvironment environment,
            ILogger<PaymentsService> logger,
            IStripeGateway stripeGateway)
        {
            this.paymentRepository = paymentRepository;
            this.paymentStatusRepository = paymentStatusRepository;
            this.bookingRepository = bookingRepository;
            this.bookingStatusRepository = bookingStatusRepository;
            this.emailSender = emailSender;
            this.configuration = configuration;
            this.environment = environment;
            this.logger = logger;
            this.stripeGateway = stripeGateway;
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

        public async Task<Guid?> ConfirmCheckoutAsync(string checkoutSessionId)
        {
            if (string.IsNullOrWhiteSpace(checkoutSessionId))
            {
                return null;
            }

            Payment payment = await this.paymentRepository.All()
                .Include(x => x.Status)
                .FirstOrDefaultAsync(x => x.CheckoutSessionId == checkoutSessionId);

            if (payment == null)
            {
                return null;
            }

            var statusName = payment.Status?.Name;

            if (payment.Provider == MockProvider)
            {
                // The pretend payment counts only where the pretend payment is allowed. With a
                // real key in place, an address typed with a pretend id confirms nothing.
                if (statusName == "Pending" && this.MockPaymentsAllowed())
                {
                    await this.ProcessPaymentSuccessAsync(checkoutSessionId, $"txn_mock_{Guid.NewGuid()}");
                }
            }
            else if (statusName == "Pending" || statusName == "Cancelled")
            {
                // The site never takes the browser's word that a page was paid: an address with
                // the page's id in it can be typed by anyone who has seen that page. Stripe is
                // asked. A payment the site had given up on is asked about too, so that money
                // which did reach Stripe is written down whatever the site thought.
                Session session = await this.stripeGateway.GetCheckoutAsync(checkoutSessionId);
                if (IsPaid(session))
                {
                    await this.ProcessPaymentSuccessAsync(checkoutSessionId, session.PaymentIntentId);
                }
            }

            return payment.BookingId;
        }

        // Writes down a deposit that has been paid. It asks nobody whether that is true: the
        // customer's return from Stripe and Stripe's own message both come here through
        // ConfirmCheckoutAsync, which asks Stripe first, and it is not on the interface for
        // that reason.
        public async Task ProcessPaymentSuccessAsync(string checkoutSessionId, string transactionId)
        {
            Payment payment = await this.paymentRepository.All()
                .Include(x => x.Booking).ThenInclude(b => b.BookingServices).ThenInclude(bs => bs.Service)
                .Include(x => x.Booking).ThenInclude(b => b.AvailabilitySlot)
                .Include(x => x.Booking).ThenInclude(b => b.Status)
                .FirstOrDefaultAsync(x => x.CheckoutSessionId == checkoutSessionId);

            if (payment == null)
            {
                return;
            }

            PaymentStatus paidStatus = await this.paymentStatusRepository.All().FirstOrDefaultAsync(x => x.Name == "DepositPaid");
            if (paidStatus == null)
            {
                throw new InvalidOperationException("Payment status 'DepositPaid' is not seeded.");
            }

            // Stripe's message and the customer's return can both arrive for the same payment.
            // The first one writes it down and sends the emails; the second finds it done and
            // changes nothing, the transaction id included.
            if (payment.StatusId == paidStatus.Id)
            {
                return;
            }

            payment.TransactionId = transactionId;
            payment.StatusId = paidStatus.Id;

            Booking booking = payment.Booking;
            if (booking == null)
            {
                await this.paymentRepository.SaveChangesAsync();
                return;
            }

            if (booking.Status?.Name != "Pending")
            {
                // Money for a booking that is not waiting for it: one that was dropped or
                // cancelled while a payment page was still open, or one paid twice. The site
                // closes those pages itself (CloseCheckoutsAsync), so this should not happen,
                // but money is never left unwritten, and it never switches a booking back on:
                // its hour may be someone else's by now. The company is told and decides.
                booking.History.Add(JobHistory.Line($"Deposit of {JobHistory.Pounds(payment.Amount)} paid on the website when the booking was not waiting for one. Nothing else was changed: book the job again or send the deposit back in Stripe."));
                await this.paymentRepository.SaveChangesAsync();

                this.logger.LogWarning(
                    "A deposit was paid for booking {BookingId}, which was {Status} and not waiting for one",
                    booking.Id,
                    booking.Status?.Name);
                await this.SendUnexpectedDepositNoticeAsync(booking, payment);
                return;
            }

            BookingStatus approvedStatus = await this.bookingStatusRepository.All().FirstOrDefaultAsync(x => x.Name == "Approved");
            if (approvedStatus != null)
            {
                booking.StatusId = approvedStatus.Id;
            }

            booking.History.Add(JobHistory.Line($"Deposit of {JobHistory.Pounds(payment.Amount)} paid on the website."));

            await this.paymentRepository.SaveChangesAsync();

            await this.SendBookingConfirmationEmailsAsync(booking, payment);
        }

        public async Task<CheckoutClosure> CloseCheckoutsAsync(Guid bookingId)
        {
            PaymentStatus cancelledStatus = await this.paymentStatusRepository.All().FirstOrDefaultAsync(x => x.Name == "Cancelled");
            List<Payment> open = await this.paymentRepository.All()
                .Where(x => x.BookingId == bookingId && x.Status.Name == "Pending")
                .ToListAsync();

            if (cancelledStatus == null || open.Count == 0)
            {
                return CheckoutClosure.Closed;
            }

            CheckoutClosure result = CheckoutClosure.Closed;

            foreach (Payment payment in open)
            {
                if (payment.Provider != StripeProvider)
                {
                    // The pretend payment has no page anywhere to close.
                    payment.StatusId = cancelledStatus.Id;
                    continue;
                }

                try
                {
                    Session session = await this.stripeGateway.ExpireCheckoutAsync(payment.CheckoutSessionId);
                    if (IsPaid(session))
                    {
                        // Paid in the moment before it was closed. That is a booking, not a
                        // booking to drop.
                        await this.ProcessPaymentSuccessAsync(payment.CheckoutSessionId, session.PaymentIntentId);
                        return CheckoutClosure.Paid;
                    }

                    payment.StatusId = cancelledStatus.Id;
                }
                catch (Exception ex)
                {
                    // Stripe could not be reached. The page may still be open, so the payment
                    // stays as it is and whoever asked is told, to try again later.
                    this.logger.LogWarning(ex, "The payment page of booking {BookingId} could not be closed at Stripe", bookingId);
                    result = CheckoutClosure.Unknown;
                }
            }

            await this.paymentRepository.SaveChangesAsync();
            return result;
        }

        // A payment the admin writes on a job: how the rest was paid, in person or by transfer.
        // Every payment is one line on the job, so its page always shows what is still owed
        // (PROJECT_STATE.md Section 3ce). The website deposit is not written in here: the site
        // records that one by itself, above.
        public async Task<bool> AddPaymentAsync(Guid bookingId, decimal amount, string method)
        {
            if (amount <= 0m || amount > 100000m || !PaymentMethods.All.Contains(method))
            {
                return false;
            }

            Booking booking = await this.bookingRepository.All()
                .Include(x => x.Status)
                .Include(x => x.Payments).ThenInclude(p => p.Status)
                .FirstOrDefaultAsync(x => x.Id == bookingId);
            PaymentStatus completedStatus = await this.paymentStatusRepository.All().FirstOrDefaultAsync(x => x.Name == "Completed");

            if (booking == null
                || completedStatus == null
                || !BookingRules.CanTakePayment(booking.Status?.Name, booking.Source == BookingSource.Website, HasPayment(booking, "DepositPaid")))
            {
                return false;
            }

            await this.paymentRepository.AddAsync(new Payment
            {
                BookingId = bookingId,
                Amount = amount,
                Provider = "Manual",
                Method = method,
                StatusId = completedStatus.Id,
            });
            booking.History.Add(JobHistory.Line($"Payment of {JobHistory.Pounds(amount)} written on the job: {method}."));

            await this.paymentRepository.SaveChangesAsync();
            return true;
        }

        // For a line typed in by mistake. Only a payment the admin wrote can be taken off; the
        // deposit paid on the website is what Stripe says it is.
        public async Task<bool> RemovePaymentAsync(Guid bookingId, Guid paymentId)
        {
            Payment payment = await this.paymentRepository.All()
                .Include(x => x.Status)
                .Include(x => x.Booking)
                .FirstOrDefaultAsync(x => x.Id == paymentId && x.BookingId == bookingId);

            if (payment == null || payment.Status?.Name != "Completed" || payment.Booking == null)
            {
                return false;
            }

            this.paymentRepository.Delete(payment);
            payment.Booking.History.Add(JobHistory.Line($"Payment of {JobHistory.Pounds(payment.Amount)} ({payment.Method}) taken off the job."));

            await this.paymentRepository.SaveChangesAsync();
            return true;
        }

        // The tick on a cancelled job that says its deposit went back. The refund itself is made
        // by hand in Stripe; this is the record that it was. A deposit marked refunded no longer
        // counts as money in, on the job or in the revenue figure.
        public async Task<bool> SetDepositRefundedAsync(Guid bookingId, bool refunded)
        {
            Booking booking = await this.bookingRepository.All()
                .Include(x => x.Status)
                .Include(x => x.Payments).ThenInclude(p => p.Status)
                .FirstOrDefaultAsync(x => x.Id == bookingId);
            PaymentStatus paidStatus = await this.paymentStatusRepository.All().FirstOrDefaultAsync(x => x.Name == "DepositPaid");
            PaymentStatus refundedStatus = await this.paymentStatusRepository.All().FirstOrDefaultAsync(x => x.Name == "Refunded");

            if (booking == null
                || paidStatus == null
                || refundedStatus == null
                || !BookingRules.CanMarkDepositRefunded(booking.Status?.Name, HasPayment(booking, "DepositPaid") || HasPayment(booking, "Refunded")))
            {
                return false;
            }

            Payment deposit = booking.Payments.FirstOrDefault(p => p.Status != null && p.Status.Name == (refunded ? "DepositPaid" : "Refunded"));
            if (deposit == null)
            {
                // It already stands the way it was asked to.
                return true;
            }

            deposit.StatusId = refunded ? refundedStatus.Id : paidStatus.Id;
            booking.History.Add(JobHistory.Line(refunded ? "Deposit marked as refunded." : "Deposit marked as not refunded."));

            await this.paymentRepository.SaveChangesAsync();
            return true;
        }

        // What came in and stayed, and a deposit that was sent back, oldest first. Payments a
        // customer started on the website and never finished are not money and are left out.
        public async Task<IEnumerable<T>> GetMoneyListAsync<T>(Guid bookingId)
        {
            return await this.paymentRepository.All()
                .Where(x => x.BookingId == bookingId)
                .Where(x => x.Status.Name == "DepositPaid" || x.Status.Name == "Completed" || x.Status.Name == "Refunded")
                .OrderBy(x => x.CreatedOn)
                .To<T>()
                .ToListAsync();
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
            var deposit = $"&pound;{payment.Amount:F2}";
            var clientSubject = "Your Plumbing Handyman Surrey Booking is Confirmed!";
            var clientBody = EmailLayout.ForCustomer(
                EmailLayout.Badge(EmailColour.Green, "&#10003; Deposit Received &amp; Booking Confirmed")
                + EmailLayout.Heading($"Hi {EmailText.Encode(booking.CustomerFirstName)},")
                + EmailLayout.Lead($"Great news! Your deposit of <strong>{deposit}</strong> has been received via Stripe and your booking is now locked in our schedule.")
                + EmailLayout.Details(
                    EmailLayout.ReferenceRow(reference),
                    EmailLayout.Row("Service(s)", serviceNames),
                    EmailLayout.TimeRow("Scheduled Time", scheduledTime),
                    EmailLayout.PlainRow("Address", address),
                    EmailLayout.Divider(),
                    EmailLayout.MoneyRow("Deposit Paid", $"{deposit} (Paid)"))
                + EmailLayout.Text("<strong>What happens next?</strong><br />We will send you a follow-up email with your assigned technician's name and direct contact number as soon as one is allocated. If you need to send photos of the job or additional directions, feel free to message us on WhatsApp below.")
                + EmailLayout.WhatsAppButton("Message Us on WhatsApp"));

            await this.emailSender.TrySendEmailAsync(
                this.logger,
                "deposit paid, to the customer",
                EmailSettings.BookingsFromAddress(this.configuration),
                EmailSettings.CustomerFromName,
                booking.Email,
                clientSubject,
                clientBody);

            var adminSubject = $"New Confirmed Booking - {booking.CustomerFirstName} {booking.CustomerLastName}";
            var adminBody = EmailLayout.ForCompany(
                EmailLayout.Badge(EmailColour.Green, "&#128276; New Customer Booking")
                + EmailLayout.Heading("A booking deposit has just been paid.")
                + EmailLayout.Lead("A customer has completed payment for their booking deposit via Stripe. The slot is confirmed in the schedule.")
                + EmailLayout.Details(
                    EmailLayout.ReferenceRow(reference),
                    EmailLayout.Row("Customer", customerName),
                    EmailLayout.PlainRow("Customer Contact", ContactLinks(booking)),
                    EmailLayout.Row("Service(s)", serviceNames),
                    EmailLayout.TimeRow("Scheduled Time", scheduledTime),
                    EmailLayout.PlainRow("Service Address", address),
                    EmailLayout.Divider(),
                    EmailLayout.MoneyRow("Deposit Paid", $"{deposit} (Stripe)"))
                + EmailLayout.Advice($"<strong>Next Step:</strong> Open the booking in the admin panel and assign a technician. The customer will be sent an automated notification with the technician's name as soon as you save.<br /><br />{ReplyTip}")
                + EmailLayout.Button("&#9881;&#65039; Open Booking in Admin Panel", this.JobPage(booking)));

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

        // To the company only. The customer is not told the booking is confirmed, because it is
        // not: someone has to ring them, and either find a new time or send the money back.
        private async Task SendUnexpectedDepositNoticeAsync(Booking booking, Payment payment)
        {
            var customerName = EmailText.Encode($"{booking.CustomerFirstName} {booking.CustomerLastName}");
            var scheduledTime = booking.ScheduledStart.HasValue
                ? booking.ScheduledStart.Value.ToString("dd MMM yyyy 'at' HH:mm")
                : "not recorded";

            var subject = $"Action needed: deposit paid for a booking that is not held - {booking.CustomerFirstName} {booking.CustomerLastName}";
            var body = EmailLayout.ForCompany(
                EmailLayout.Badge(EmailColour.Amber, "&#9888;&#65039; Action Needed")
                + EmailLayout.Heading("A deposit was paid for a booking that was not waiting for one.")
                + EmailLayout.Lead("The booking had been dropped or cancelled, or was already paid, when this money arrived. The site has written the payment on the job and changed nothing else. The customer has not been sent a confirmation.")
                + EmailLayout.Details(
                    EmailLayout.ReferenceRow(BookingReference.Short(booking.Id)),
                    EmailLayout.Row("Customer", customerName),
                    EmailLayout.PlainRow("Customer Contact", ContactLinks(booking)),
                    EmailLayout.TimeRow("It was booked for", scheduledTime),
                    EmailLayout.Divider(),
                    EmailLayout.MoneyRow("Deposit Paid", $"&pound;{payment.Amount:F2}"))
                + EmailLayout.Advice($"<strong>Next Step:</strong> Call the customer. The site has told them you will be in touch to confirm their visit. Either write the job in again for a time that is free, or send the deposit back in Stripe.<br /><br />{ReplyTip}")
                + EmailLayout.Button("&#9881;&#65039; Open Booking in Admin Panel", this.JobPage(booking)));

            await this.emailSender.TrySendEmailAsync(
                this.logger,
                "deposit paid for a booking not held, notice to the company",
                EmailSettings.SystemFromAddress(this.configuration),
                EmailSettings.WebsiteFromName,
                EmailSettings.AdminNotificationAddress(this.configuration),
                subject,
                body,
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

        public async Task<PaymentCheckoutResult> CreateCheckoutSessionAsync(Guid bookingId, string successUrl, string cancelUrl)
        {
            Booking booking = await this.bookingRepository.All()
                .Include(x => x.Status)
                .Include(x => x.Payments).ThenInclude(p => p.Status)
                .FirstOrDefaultAsync(x => x.Id == bookingId);

            // The address that opens a payment page has the booking's id in it and nothing else,
            // so it is the booking that says whether there is anything to pay: one already paid
            // would be paid twice, and one that was dropped has no hour held for it.
            if (booking == null
                || !BookingRules.CanPayDeposit(booking.Status?.Name, booking.Source == BookingSource.Website, HasPayment(booking, "DepositPaid"))
                || booking.DepositAmount.GetValueOrDefault() <= 0m)
            {
                return null;
            }

            var depositAmount = booking.DepositAmount.Value;
            var keyMissing = string.IsNullOrWhiteSpace(this.configuration["Stripe:SecretKey"]);

            if (keyMissing && !this.MockPaymentsAllowed())
            {
                // Never silently fake a payment or attempt a doomed Stripe call outside
                // development: fail loudly so a missing production secret gets noticed.
                throw new InvalidOperationException("Stripe is not configured for this environment. Set Stripe:SecretKey before accepting real payments.");
            }

            // A second try closes the page of the first, or the customer could pay on both.
            CheckoutClosure earlierPages = await this.CloseCheckoutsAsync(bookingId);
            if (earlierPages == CheckoutClosure.Paid)
            {
                return null;
            }

            if (earlierPages == CheckoutClosure.Unknown)
            {
                throw new InvalidOperationException("An earlier payment page for this booking could not be closed at Stripe, so a new one was not opened.");
            }

            if (keyMissing)
            {
                var mockSessionId = $"mock_session_{Guid.NewGuid()}";
                await this.CreatePaymentRecordAsync(bookingId, depositAmount, MockProvider, mockSessionId);
                return new PaymentCheckoutResult { IsMock = true, SessionId = mockSessionId };
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
                            UnitAmount = (long)(depositAmount * 100), // convert to pence
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
                ExpiresAt = DateTime.UtcNow.Add(CheckoutLifetime),

                // Filled in on Stripe's page, so the customer does not type it a second time
                // and Stripe's receipt goes where the site's own emails go.
                CustomerEmail = string.IsNullOrWhiteSpace(booking.Email) ? null : booking.Email,

                // The booking's id on the payment, for finding one from the other in Stripe.
                ClientReferenceId = bookingId.ToString(),
            };

            Session session = await this.stripeGateway.CreateCheckoutAsync(options);

            await this.CreatePaymentRecordAsync(bookingId, depositAmount, StripeProvider, session.Id);

            return new PaymentCheckoutResult { IsMock = false, SessionId = session.Id, RedirectUrl = session.Url };
        }

        public async Task HandleWebhookEventAsync(string json, string signature)
        {
            Event stripeEvent = this.stripeGateway.ReadEvent(json, signature, this.configuration["Stripe:WebhookSecret"]);

            var sessionId = (stripeEvent.Data?.Object as Session)?.Id;

            // One line per message, so the log can answer "is Stripe reaching us?" without
            // anyone opening the Stripe dashboard.
            this.logger.LogInformation("Stripe webhook: {EventType} for payment page {SessionId}", stripeEvent.Type, sessionId);

            if (sessionId == null)
            {
                return;
            }

            if (stripeEvent.Type == EventTypes.CheckoutSessionCompleted)
            {
                // The message says which page; whether it was paid is asked of Stripe, the
                // same way as when the customer comes back from it.
                await this.ConfirmCheckoutAsync(sessionId);
            }
            else if (stripeEvent.Type == EventTypes.CheckoutSessionExpired)
            {
                await this.CancelPaymentAsync(sessionId);
            }
        }

        // The customer's phone number and email address, each as a link the company can press.
        private static string ContactLinks(Booking booking)
        {
            return EmailLayout.Link("tel:" + EmailText.PhoneLink(booking.PhoneNumber), EmailText.Encode(booking.PhoneNumber))
                + " &bull; "
                + EmailLayout.Link("mailto:" + EmailText.Encode(booking.Email), EmailText.Encode(booking.Email));
        }

        private static bool IsPaid(Session session)
        {
            return session != null && session.PaymentStatus == "paid";
        }

        // The booking's payments have to be loaded with their statuses for this to say anything.
        private static bool HasPayment(Booking booking, string statusName)
        {
            return booking.Payments != null && booking.Payments.Any(p => p.Status != null && p.Status.Name == statusName);
        }

        // The pretend payment stands in for Stripe so the booking flow can be gone through
        // without an account: always in Development, and outside it only where a setting asks
        // for it by name (Stripe:AllowSandboxOutsideDevelopment), which the live site must never
        // have. A real key switches it off everywhere.
        private bool MockPaymentsAllowed()
        {
            return string.IsNullOrWhiteSpace(this.configuration["Stripe:SecretKey"])
                && (this.environment.IsDevelopment() || this.configuration.GetValue<bool>("Stripe:AllowSandboxOutsideDevelopment"));
        }

        // The job's own page in the admin panel of the site that sent the notice: staging's
        // notices open staging, the live site's open the live site.
        private string JobPage(Booking booking)
        {
            return $"{EmailSettings.SiteUrl(this.configuration)}/Administration/Bookings/Details/{booking.Id}";
        }
    }
}
