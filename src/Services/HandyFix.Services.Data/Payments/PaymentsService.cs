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
    using HandyFix.Services.Data.Emails;
    using HandyFix.Services.Mapping;
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

        // Stripe keeps a payment page open for a day unless told otherwise, and will not take
        // less than thirty minutes. The site closes the page itself when it drops the booking
        // (CloseCheckoutsAsync); this is what is left if that call never gets through.
        private static readonly TimeSpan CheckoutLifetime = TimeSpan.FromMinutes(31);

        private readonly IDeletableEntityRepository<Payment> paymentRepository;
        private readonly IDeletableEntityRepository<PaymentStatus> paymentStatusRepository;
        private readonly IDeletableEntityRepository<Booking> bookingRepository;
        private readonly IDeletableEntityRepository<BookingStatus> bookingStatusRepository;
        private readonly IBookingEmails bookingEmails;
        private readonly IConfiguration configuration;
        private readonly IWebHostEnvironment environment;
        private readonly ILogger<PaymentsService> logger;
        private readonly IStripeGateway stripeGateway;

        public PaymentsService(
            IDeletableEntityRepository<Payment> paymentRepository,
            IDeletableEntityRepository<PaymentStatus> paymentStatusRepository,
            IDeletableEntityRepository<Booking> bookingRepository,
            IDeletableEntityRepository<BookingStatus> bookingStatusRepository,
            IBookingEmails bookingEmails,
            IConfiguration configuration,
            IWebHostEnvironment environment,
            ILogger<PaymentsService> logger,
            IStripeGateway stripeGateway)
        {
            this.paymentRepository = paymentRepository;
            this.paymentStatusRepository = paymentStatusRepository;
            this.bookingRepository = bookingRepository;
            this.bookingStatusRepository = bookingStatusRepository;
            this.bookingEmails = bookingEmails;
            this.configuration = configuration;
            this.environment = environment;
            this.logger = logger;
            this.stripeGateway = stripeGateway;
        }

        public async Task<Guid> CreatePaymentRecordAsync(Guid bookingId, decimal amount, string provider, string checkoutSessionId)
        {
            PaymentStatus pendingStatus = await this.paymentStatusRepository.All().FirstOrDefaultAsync(x => x.Name == PaymentStatusNames.Pending);
            if (pendingStatus == null)
            {
                throw new InvalidOperationException($"Payment status '{PaymentStatusNames.Pending}' is not seeded.");
            }

            // Supersede any earlier, still-pending payment attempt for this booking so a
            // customer retrying checkout doesn't pile up orphaned Pending rows pointing
            // at abandoned Stripe sessions.
            PaymentStatus cancelledStatus = await this.paymentStatusRepository.All().FirstOrDefaultAsync(x => x.Name == PaymentStatusNames.Cancelled);
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
                if (statusName == PaymentStatusNames.Pending && this.MockPaymentsAllowed())
                {
                    await this.ProcessPaymentSuccessAsync(checkoutSessionId, $"txn_mock_{Guid.NewGuid()}");
                }
            }
            else if (statusName == PaymentStatusNames.Pending || statusName == PaymentStatusNames.Cancelled)
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

            PaymentStatus paidStatus = await this.paymentStatusRepository.All().FirstOrDefaultAsync(x => x.Name == PaymentStatusNames.DepositPaid);
            if (paidStatus == null)
            {
                throw new InvalidOperationException($"Payment status '{PaymentStatusNames.DepositPaid}' is not seeded.");
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

            if (booking.Status?.Name != BookingStatusNames.Pending)
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

            BookingStatus approvedStatus = await this.bookingStatusRepository.All().FirstOrDefaultAsync(x => x.Name == BookingStatusNames.Approved);
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
            PaymentStatus cancelledStatus = await this.paymentStatusRepository.All().FirstOrDefaultAsync(x => x.Name == PaymentStatusNames.Cancelled);
            List<Payment> open = await this.paymentRepository.All()
                .Where(x => x.BookingId == bookingId && x.Status.Name == PaymentStatusNames.Pending)
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
            PaymentStatus completedStatus = await this.paymentStatusRepository.All().FirstOrDefaultAsync(x => x.Name == PaymentStatusNames.Completed);

            if (booking == null
                || completedStatus == null
                || !BookingRules.CanTakePayment(booking.Status?.Name, booking.Source == BookingSource.Website, HasPayment(booking, PaymentStatusNames.DepositPaid)))
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

            if (payment == null || payment.Status?.Name != PaymentStatusNames.Completed || payment.Booking == null)
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
            PaymentStatus paidStatus = await this.paymentStatusRepository.All().FirstOrDefaultAsync(x => x.Name == PaymentStatusNames.DepositPaid);
            PaymentStatus refundedStatus = await this.paymentStatusRepository.All().FirstOrDefaultAsync(x => x.Name == PaymentStatusNames.Refunded);

            if (booking == null
                || paidStatus == null
                || refundedStatus == null
                || !BookingRules.CanMarkDepositRefunded(booking.Status?.Name, HasPayment(booking, PaymentStatusNames.DepositPaid) || HasPayment(booking, PaymentStatusNames.Refunded)))
            {
                return false;
            }

            Payment deposit = booking.Payments.FirstOrDefault(p => p.Status != null && p.Status.Name == (refunded ? PaymentStatusNames.DepositPaid : PaymentStatusNames.Refunded));
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
                .Where(x => x.Status.Name == PaymentStatusNames.DepositPaid || x.Status.Name == PaymentStatusNames.Completed || x.Status.Name == PaymentStatusNames.Refunded)
                .OrderBy(x => x.CreatedOn)
                .To<T>()
                .ToListAsync();
        }

        public async Task CancelPaymentAsync(string checkoutSessionId)
        {
            Payment payment = await this.paymentRepository.All()
                .FirstOrDefaultAsync(x => x.CheckoutSessionId == checkoutSessionId);
            if (payment == null)
            {
                return;
            }

            PaymentStatus pendingStatus = await this.paymentStatusRepository.All().FirstOrDefaultAsync(x => x.Name == PaymentStatusNames.Pending);
            PaymentStatus cancelledStatus = await this.paymentStatusRepository.All().FirstOrDefaultAsync(x => x.Name == PaymentStatusNames.Cancelled);
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
            PaymentStatus pendingStatus = await this.paymentStatusRepository.All().FirstOrDefaultAsync(x => x.Name == PaymentStatusNames.Pending);
            PaymentStatus cancelledStatus = await this.paymentStatusRepository.All().FirstOrDefaultAsync(x => x.Name == PaymentStatusNames.Cancelled);
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

        public async Task<decimal> GetTotalRevenueAsync()
        {
            return await this.paymentRepository.All()
                .Where(x => x.Status.Name == PaymentStatusNames.DepositPaid || x.Status.Name == PaymentStatusNames.Completed)
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
                || !BookingRules.CanPayDeposit(booking.Status?.Name, booking.Source == BookingSource.Website, HasPayment(booking, PaymentStatusNames.DepositPaid))
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

        // The words of both emails are in BookingEmails; what is written on the job when one of
        // them is not sent is decided here. The deposit is paid and the booking approved by now,
        // so neither send throws: a failed one is logged, and the other email still goes
        // (PROJECT_STATE Section 3cb).
        private async Task SendBookingConfirmationEmailsAsync(Booking booking, Payment payment)
        {
            var customerTold = await this.bookingEmails.DepositPaidToCustomerAsync(booking, payment);
            var companyTold = await this.bookingEmails.DepositPaidToCompanyAsync(booking, payment);

            // A failed send used to be a line in the server's log and nothing else: nobody knew
            // to tell the customer another way, or that a deposit had come in. It is now on the
            // job's history and in the dashboard's list (PROJECT_STATE.md Section 3cq).
            if (!customerTold)
            {
                booking.History.Add(JobHistory.Line("The email confirming the deposit could not be sent to the customer. Tell them another way that the booking is confirmed."));
                booking.EmailsNotSent.Add(new EmailNotSent { What = "The deposit confirmation, to the customer" });
            }

            if (!companyTold)
            {
                booking.History.Add(JobHistory.Line("The email telling the company of this deposit could not be sent."));
                booking.EmailsNotSent.Add(new EmailNotSent { What = "The notice of a paid deposit, to the company" });
            }

            if (!customerTold || !companyTold)
            {
                await this.paymentRepository.SaveChangesAsync();
            }
        }

        // To the company only. The customer is not told the booking is confirmed, because it is
        // not: someone has to ring them, and either find a new time or send the money back.
        private async Task SendUnexpectedDepositNoticeAsync(Booking booking, Payment payment)
        {
            var companyTold = await this.bookingEmails.DepositForBookingNotHeldAsync(booking, payment);

            if (!companyTold)
            {
                booking.History.Add(JobHistory.Line("The email telling the company of this deposit could not be sent."));
                booking.EmailsNotSent.Add(new EmailNotSent { What = "The notice of a deposit for a booking not held, to the company" });
                await this.paymentRepository.SaveChangesAsync();
            }
        }
    }
}
