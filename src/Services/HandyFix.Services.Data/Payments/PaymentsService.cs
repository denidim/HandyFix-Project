namespace HandyFix.Services.Data.Payments
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Data.Common.Repositories;
    using HandyFix.Data.Models;
    using HandyFix.Services.Mapping;

    using Microsoft.EntityFrameworkCore;

    public class PaymentsService : IPaymentsService
    {
        private readonly IDeletableEntityRepository<Payment> paymentRepository;
        private readonly IDeletableEntityRepository<PaymentStatus> paymentStatusRepository;
        private readonly IDeletableEntityRepository<Booking> bookingRepository;
        private readonly IDeletableEntityRepository<BookingStatus> bookingStatusRepository;

        public PaymentsService(
            IDeletableEntityRepository<Payment> paymentRepository,
            IDeletableEntityRepository<PaymentStatus> paymentStatusRepository,
            IDeletableEntityRepository<Booking> bookingRepository,
            IDeletableEntityRepository<BookingStatus> bookingStatusRepository)
        {
            this.paymentRepository = paymentRepository;
            this.paymentStatusRepository = paymentStatusRepository;
            this.bookingRepository = bookingRepository;
            this.bookingStatusRepository = bookingStatusRepository;
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
                .Include(x => x.Booking)
                .FirstOrDefaultAsync(x => x.CheckoutSessionId == checkoutSessionId);

            if (payment == null)
            {
                return;
            }

            // Set transaction ID
            payment.TransactionId = transactionId;

            // Change payment status to "DepositPaid"
            var paidStatus = await this.paymentStatusRepository.All().FirstOrDefaultAsync(x => x.Name == "DepositPaid");
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
