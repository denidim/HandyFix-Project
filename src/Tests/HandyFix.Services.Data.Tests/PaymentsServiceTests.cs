namespace HandyFix.Services.Data.Tests
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Data;
    using HandyFix.Data.Models;
    using HandyFix.Data.Repositories;
    using HandyFix.Services.Data.Payments;

    using Microsoft.EntityFrameworkCore;

    using Xunit;

    public class PaymentsServiceTests
    {
        [Fact]
        public async Task ProcessPaymentSuccessAsyncShouldUpdatePaymentAndBookingCorrectly()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;
            
            using var dbContext = new ApplicationDbContext(options);
            using var paymentRepo = new EfDeletableEntityRepository<Payment>(dbContext);
            using var paymentStatusRepo = new EfDeletableEntityRepository<PaymentStatus>(dbContext);
            using var bookingRepo = new EfDeletableEntityRepository<Booking>(dbContext);
            using var bookingStatusRepo = new EfDeletableEntityRepository<BookingStatus>(dbContext);

            // Seed statuses
            var pendingPaymentStatus = new PaymentStatus { Id = Guid.NewGuid(), Name = "Pending" };
            var paidPaymentStatus = new PaymentStatus { Id = Guid.NewGuid(), Name = "DepositPaid" };
            dbContext.PaymentStatuses.Add(pendingPaymentStatus);
            dbContext.PaymentStatuses.Add(paidPaymentStatus);

            var pendingBookingStatus = new BookingStatus { Id = Guid.NewGuid(), Name = "Pending" };
            var approvedBookingStatus = new BookingStatus { Id = Guid.NewGuid(), Name = "Approved" };
            dbContext.BookingStatuses.Add(pendingBookingStatus);
            dbContext.BookingStatuses.Add(approvedBookingStatus);

            // Seed booking and payment record
            var booking = new Booking
            {
                Id = Guid.NewGuid(),
                CustomerFirstName = "John",
                CustomerLastName = "Doe",
                Email = "john@example.com",
                PhoneNumber = "07123",
                Address = "12 Main Rd",
                ProblemDescription = "Leak",
                StatusId = pendingBookingStatus.Id
            };
            dbContext.Bookings.Add(booking);

            var checkoutSessionId = "cs_test_12345";
            var payment = new Payment
            {
                Id = Guid.NewGuid(),
                BookingId = booking.Id,
                Amount = 50.00m,
                Provider = "Stripe",
                CheckoutSessionId = checkoutSessionId,
                StatusId = pendingPaymentStatus.Id
            };
            dbContext.Payments.Add(payment);
            await dbContext.SaveChangesAsync();

            var service = new PaymentsService(paymentRepo, paymentStatusRepo, bookingRepo, bookingStatusRepo);
            await service.ProcessPaymentSuccessAsync(checkoutSessionId, "txn_stripe_9999");

            // Verify payment update
            var updatedPayment = dbContext.Payments.First(x => x.Id == payment.Id);
            Assert.Equal("txn_stripe_9999", updatedPayment.TransactionId);
            Assert.Equal(paidPaymentStatus.Id, updatedPayment.StatusId);

            // Verify associated booking update
            var updatedBooking = dbContext.Bookings.First(x => x.Id == booking.Id);
            Assert.Equal(approvedBookingStatus.Id, updatedBooking.StatusId);
        }
    }
}
