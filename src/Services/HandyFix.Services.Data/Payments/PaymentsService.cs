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

    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Configuration;

    public class PaymentsService : IPaymentsService
    {
        private const string DefaultAdminNotificationEmail = "admin@handyfix.co.uk";

        private readonly IDeletableEntityRepository<Payment> paymentRepository;
        private readonly IDeletableEntityRepository<PaymentStatus> paymentStatusRepository;
        private readonly IDeletableEntityRepository<Booking> bookingRepository;
        private readonly IDeletableEntityRepository<BookingStatus> bookingStatusRepository;
        private readonly IEmailSender emailSender;
        private readonly IConfiguration configuration;

        public PaymentsService(
            IDeletableEntityRepository<Payment> paymentRepository,
            IDeletableEntityRepository<PaymentStatus> paymentStatusRepository,
            IDeletableEntityRepository<Booking> bookingRepository,
            IDeletableEntityRepository<BookingStatus> bookingStatusRepository,
            IEmailSender emailSender,
            IConfiguration configuration)
        {
            this.paymentRepository = paymentRepository;
            this.paymentStatusRepository = paymentStatusRepository;
            this.bookingRepository = bookingRepository;
            this.bookingStatusRepository = bookingStatusRepository;
            this.emailSender = emailSender;
            this.configuration = configuration;
        }

        public async Task<Guid> CreatePaymentRecordAsync(Guid bookingId, decimal amount, string provider, string checkoutSessionId)
        {
            var pendingStatus = await this.paymentStatusRepository.All().FirstOrDefaultAsync(x => x.Name == "Pending");
            if (pendingStatus == null)
            {
                throw new InvalidOperationException("Payment status 'Pending' is not seeded.");
            }

            // Supersede any earlier, still-pending payment attempt for this booking so a
            // customer retrying checkout doesn't pile up orphaned Pending rows pointing
            // at abandoned Stripe sessions.
            var cancelledStatus = await this.paymentStatusRepository.All().FirstOrDefaultAsync(x => x.Name == "Cancelled");
            if (cancelledStatus != null)
            {
                var existingPendingPayments = await this.paymentRepository.All()
                    .Where(x => x.BookingId == bookingId && x.StatusId == pendingStatus.Id)
                    .ToListAsync();

                foreach (var existing in existingPendingPayments)
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
            var payment = await this.paymentRepository.All()
                .Include(x => x.Booking).ThenInclude(b => b.BookingServices).ThenInclude(bs => bs.Service)
                .Include(x => x.Booking).ThenInclude(b => b.AvailabilitySlot)
                .Include(x => x.Booking).ThenInclude(b => b.Technician)
                .FirstOrDefaultAsync(x => x.CheckoutSessionId == checkoutSessionId);

            if (payment == null)
            {
                return;
            }

            var paidStatus = await this.paymentStatusRepository.All().FirstOrDefaultAsync(x => x.Name == "DepositPaid");

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
            var booking = payment.Booking;
            if (booking != null)
            {
                var approvedStatus = await this.bookingStatusRepository.All().FirstOrDefaultAsync(x => x.Name == "Approved");
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
            var technicianName = booking.Technician != null
                ? $"{booking.Technician.FirstName} {booking.Technician.LastName}"
                : "Not yet assigned";

            var clientSubject = "Your HandyFix Booking is Confirmed!";
            var clientBody = $@"
                <h3>Hi {booking.CustomerFirstName},</h3>
                <p>Great news — your deposit of £{payment.Amount:F2} has been received and your booking is now confirmed.</p>
                <ul>
                    <li><strong>Booking Reference:</strong> {booking.Id}</li>
                    <li><strong>Service(s):</strong> {serviceNames}</li>
                    <li><strong>Scheduled Time:</strong> {scheduledTime}</li>
                    <li><strong>Address:</strong> {booking.Address}</li>
                    <li><strong>Technician:</strong> {technicianName}</li>
                </ul>
                <p>We look forward to helping you. Thank you for choosing HandyFix!</p>";

            await this.emailSender.SendEmailAsync(
                "bookings@handyfix.co.uk",
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
                "no-reply@handyfix.co.uk",
                "HandyFix System",
                adminEmail,
                adminSubject,
                adminBody);
        }

        public async Task CancelPaymentAsync(string checkoutSessionId)
        {
            var payment = await this.paymentRepository.All()
                .FirstOrDefaultAsync(x => x.CheckoutSessionId == checkoutSessionId);
            if (payment == null)
            {
                return;
            }

            var pendingStatus = await this.paymentStatusRepository.All().FirstOrDefaultAsync(x => x.Name == "Pending");
            var cancelledStatus = await this.paymentStatusRepository.All().FirstOrDefaultAsync(x => x.Name == "Cancelled");
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
            var pendingStatus = await this.paymentStatusRepository.All().FirstOrDefaultAsync(x => x.Name == "Pending");
            var cancelledStatus = await this.paymentStatusRepository.All().FirstOrDefaultAsync(x => x.Name == "Cancelled");
            if (pendingStatus == null || cancelledStatus == null)
            {
                return;
            }

            var idList = bookingIds?.ToList() ?? new List<Guid>();
            if (idList.Count == 0)
            {
                return;
            }

            var pendingPayments = await this.paymentRepository.All()
                .Where(x => idList.Contains(x.BookingId) && x.StatusId == pendingStatus.Id)
                .ToListAsync();

            if (pendingPayments.Count == 0)
            {
                return;
            }

            foreach (var payment in pendingPayments)
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
    }
}
