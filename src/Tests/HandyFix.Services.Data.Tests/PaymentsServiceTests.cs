namespace HandyFix.Services.Data.Tests
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Data;
    using HandyFix.Data.Models;
    using HandyFix.Data.Repositories;
    using HandyFix.Services.Data.Payments;
    using HandyFix.Services.Messaging;

    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Configuration;

    using Moq;

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

            var emailSenderMock = new Mock<IEmailSender>();
            var service = new PaymentsService(paymentRepo, paymentStatusRepo, bookingRepo, bookingStatusRepo, emailSenderMock.Object, new ConfigurationBuilder().Build());
            await service.ProcessPaymentSuccessAsync(checkoutSessionId, "txn_stripe_9999");

            // Verify payment update
            var updatedPayment = dbContext.Payments.First(x => x.Id == payment.Id);
            Assert.Equal("txn_stripe_9999", updatedPayment.TransactionId);
            Assert.Equal(paidPaymentStatus.Id, updatedPayment.StatusId);

            // Verify associated booking update
            var updatedBooking = dbContext.Bookings.First(x => x.Id == booking.Id);
            Assert.Equal(approvedBookingStatus.Id, updatedBooking.StatusId);
        }

        [Fact]
        public async Task ProcessPaymentSuccessAsyncShouldSendClientConfirmationAndAdminNotificationEmails()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;

            using var dbContext = new ApplicationDbContext(options);
            using var paymentRepo = new EfDeletableEntityRepository<Payment>(dbContext);
            using var paymentStatusRepo = new EfDeletableEntityRepository<PaymentStatus>(dbContext);
            using var bookingRepo = new EfDeletableEntityRepository<Booking>(dbContext);
            using var bookingStatusRepo = new EfDeletableEntityRepository<BookingStatus>(dbContext);

            var pendingPaymentStatus = new PaymentStatus { Id = Guid.NewGuid(), Name = "Pending" };
            var paidPaymentStatus = new PaymentStatus { Id = Guid.NewGuid(), Name = "DepositPaid" };
            dbContext.PaymentStatuses.AddRange(pendingPaymentStatus, paidPaymentStatus);

            var pendingBookingStatus = new BookingStatus { Id = Guid.NewGuid(), Name = "Pending" };
            var approvedBookingStatus = new BookingStatus { Id = Guid.NewGuid(), Name = "Approved" };
            dbContext.BookingStatuses.AddRange(pendingBookingStatus, approvedBookingStatus);

            var category = new ServiceCategory { Id = Guid.NewGuid(), Name = "Plumbing", Description = "leak repairs", Slug = "plumbing" };
            dbContext.ServiceCategories.Add(category);

            var serviceEntity = new Service
            {
                Id = Guid.NewGuid(),
                Name = "Leak Fix",
                Description = "Repair leak",
                Slug = "leak-fix",
                BasePrice = 80.00m,
                CategoryId = category.Id,
            };
            dbContext.Services.Add(serviceEntity);

            var technician = new Technician { Id = Guid.NewGuid(), FirstName = "Alex", LastName = "Smith", PhoneNumber = "07000000000" };
            dbContext.Technicians.Add(technician);

            var booking = new Booking
            {
                CustomerFirstName = "John",
                CustomerLastName = "Doe",
                Email = "john@example.com",
                PhoneNumber = "07123",
                Address = "12 Main Rd",
                ProblemDescription = "Leak",
                StatusId = pendingBookingStatus.Id,
                TechnicianId = technician.Id,
            };
            dbContext.Bookings.Add(booking);

            var slot = new AvailabilitySlot
            {
                StartTime = DateTime.Today.AddHours(9),
                EndTime = DateTime.Today.AddHours(10),
                IsBooked = true,
                BookingId = booking.Id,
                TechnicianId = technician.Id,
            };
            dbContext.AvailabilitySlots.Add(slot);

            dbContext.BookingServices.Add(new BookingService { BookingId = booking.Id, ServiceId = serviceEntity.Id, PriceAtBooking = serviceEntity.BasePrice, Quantity = 1 });

            var checkoutSessionId = "cs_test_email_dispatch";
            var payment = new Payment
            {
                BookingId = booking.Id,
                Amount = 50.00m,
                Provider = "Stripe",
                CheckoutSessionId = checkoutSessionId,
                StatusId = pendingPaymentStatus.Id,
            };
            dbContext.Payments.Add(payment);
            await dbContext.SaveChangesAsync();

            var emailSenderMock = new Mock<IEmailSender>();
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new[] { new System.Collections.Generic.KeyValuePair<string, string>("Admin:NotificationEmail", "owner@handyfix.co.uk") })
                .Build();
            var service = new PaymentsService(paymentRepo, paymentStatusRepo, bookingRepo, bookingStatusRepo, emailSenderMock.Object, configuration);

            await service.ProcessPaymentSuccessAsync(checkoutSessionId, "txn_stripe_email_test");

            emailSenderMock.Verify(
                x => x.SendEmailAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    "john@example.com",
                    It.IsAny<string>(),
                    It.Is<string>(body => body.Contains("Leak Fix") && body.Contains("Alex Smith")),
                    null),
                Times.Once);

            emailSenderMock.Verify(
                x => x.SendEmailAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    "owner@handyfix.co.uk",
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    null),
                Times.Once);
        }

        [Fact]
        public async Task ProcessPaymentSuccessAsyncShouldBeIdempotentWhenTheWebhookFiresTwice()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;

            using var dbContext = new ApplicationDbContext(options);
            using var paymentRepo = new EfDeletableEntityRepository<Payment>(dbContext);
            using var paymentStatusRepo = new EfDeletableEntityRepository<PaymentStatus>(dbContext);
            using var bookingRepo = new EfDeletableEntityRepository<Booking>(dbContext);
            using var bookingStatusRepo = new EfDeletableEntityRepository<BookingStatus>(dbContext);

            var pendingPaymentStatus = new PaymentStatus { Id = Guid.NewGuid(), Name = "Pending" };
            var paidPaymentStatus = new PaymentStatus { Id = Guid.NewGuid(), Name = "DepositPaid" };
            dbContext.PaymentStatuses.AddRange(pendingPaymentStatus, paidPaymentStatus);

            var pendingBookingStatus = new BookingStatus { Id = Guid.NewGuid(), Name = "Pending" };
            var approvedBookingStatus = new BookingStatus { Id = Guid.NewGuid(), Name = "Approved" };
            dbContext.BookingStatuses.AddRange(pendingBookingStatus, approvedBookingStatus);

            var booking = new Booking
            {
                CustomerFirstName = "John",
                CustomerLastName = "Doe",
                Email = "john@example.com",
                PhoneNumber = "07123",
                Address = "12 Main Rd",
                ProblemDescription = "Leak",
                StatusId = pendingBookingStatus.Id,
            };
            dbContext.Bookings.Add(booking);

            var checkoutSessionId = "cs_test_double_fire";
            var payment = new Payment
            {
                BookingId = booking.Id,
                Amount = 50.00m,
                Provider = "Stripe",
                CheckoutSessionId = checkoutSessionId,
                StatusId = pendingPaymentStatus.Id,
            };
            dbContext.Payments.Add(payment);
            await dbContext.SaveChangesAsync();

            var emailSenderMock = new Mock<IEmailSender>();
            var service = new PaymentsService(paymentRepo, paymentStatusRepo, bookingRepo, bookingStatusRepo, emailSenderMock.Object, new ConfigurationBuilder().Build());

            // Stripe retries webhooks; the success handler and the Success redirect can
            // also both fire for the same session. Neither should be able to corrupt state
            // or send a duplicate confirmation email.
            await service.ProcessPaymentSuccessAsync(checkoutSessionId, "txn_stripe_9999");
            await service.ProcessPaymentSuccessAsync(checkoutSessionId, "txn_stripe_9999");

            Assert.Single(dbContext.Payments.Where(x => x.BookingId == booking.Id));
            var updatedPayment = dbContext.Payments.First(x => x.Id == payment.Id);
            Assert.Equal("txn_stripe_9999", updatedPayment.TransactionId);
            Assert.Equal(paidPaymentStatus.Id, updatedPayment.StatusId);

            var updatedBooking = dbContext.Bookings.First(x => x.Id == booking.Id);
            Assert.Equal(approvedBookingStatus.Id, updatedBooking.StatusId);

            // Exactly two emails total (one client, one admin) across both calls, not four.
            emailSenderMock.Verify(
                x => x.SendEmailAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    null),
                Times.Exactly(2));

            emailSenderMock.Verify(
                x => x.SendEmailAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    "john@example.com",
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    null),
                Times.Once);

            // No admin config supplied, so the default admin mailbox should have been used.
            emailSenderMock.Verify(
                x => x.SendEmailAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    "admin@handyfix.co.uk",
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    null),
                Times.Once);
        }

        [Fact]
        public async Task CreatePaymentRecordAsyncShouldSupersedeAnExistingPendingPaymentForTheSameBooking()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;

            using var dbContext = new ApplicationDbContext(options);
            using var paymentRepo = new EfDeletableEntityRepository<Payment>(dbContext);
            using var paymentStatusRepo = new EfDeletableEntityRepository<PaymentStatus>(dbContext);
            using var bookingRepo = new EfDeletableEntityRepository<Booking>(dbContext);
            using var bookingStatusRepo = new EfDeletableEntityRepository<BookingStatus>(dbContext);

            var pendingPaymentStatus = new PaymentStatus { Id = Guid.NewGuid(), Name = "Pending" };
            var cancelledPaymentStatus = new PaymentStatus { Id = Guid.NewGuid(), Name = "Cancelled" };
            dbContext.PaymentStatuses.AddRange(pendingPaymentStatus, cancelledPaymentStatus);

            var pendingBookingStatus = new BookingStatus { Id = Guid.NewGuid(), Name = "Pending" };
            dbContext.BookingStatuses.Add(pendingBookingStatus);

            var booking = new Booking
            {
                CustomerFirstName = "John",
                CustomerLastName = "Doe",
                Email = "john@example.com",
                PhoneNumber = "07123",
                Address = "12 Main Rd",
                ProblemDescription = "Leak",
                StatusId = pendingBookingStatus.Id,
            };
            dbContext.Bookings.Add(booking);
            await dbContext.SaveChangesAsync();

            var emailSenderMock = new Mock<IEmailSender>();
            var service = new PaymentsService(paymentRepo, paymentStatusRepo, bookingRepo, bookingStatusRepo, emailSenderMock.Object, new ConfigurationBuilder().Build());

            // Customer clicks "Pay" twice (e.g. hits back and retries) before completing
            // either Stripe checkout.
            var firstPaymentId = await service.CreatePaymentRecordAsync(booking.Id, 50.00m, "Stripe", "cs_test_first");
            var secondPaymentId = await service.CreatePaymentRecordAsync(booking.Id, 50.00m, "Stripe", "cs_test_second");

            var paymentsForBooking = dbContext.Payments.Where(x => x.BookingId == booking.Id).ToList();
            Assert.Equal(2, paymentsForBooking.Count);

            var firstPayment = paymentsForBooking.First(x => x.Id == firstPaymentId);
            var secondPayment = paymentsForBooking.First(x => x.Id == secondPaymentId);

            Assert.Equal(cancelledPaymentStatus.Id, firstPayment.StatusId);
            Assert.Equal(pendingPaymentStatus.Id, secondPayment.StatusId);
        }

        [Fact]
        public async Task CancelPaymentAsyncShouldCancelAStillPendingPaymentOnSessionExpiry()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;

            using var dbContext = new ApplicationDbContext(options);
            using var paymentRepo = new EfDeletableEntityRepository<Payment>(dbContext);
            using var paymentStatusRepo = new EfDeletableEntityRepository<PaymentStatus>(dbContext);
            using var bookingRepo = new EfDeletableEntityRepository<Booking>(dbContext);
            using var bookingStatusRepo = new EfDeletableEntityRepository<BookingStatus>(dbContext);

            var pendingPaymentStatus = new PaymentStatus { Id = Guid.NewGuid(), Name = "Pending" };
            var cancelledPaymentStatus = new PaymentStatus { Id = Guid.NewGuid(), Name = "Cancelled" };
            dbContext.PaymentStatuses.AddRange(pendingPaymentStatus, cancelledPaymentStatus);

            var checkoutSessionId = "cs_test_expired";
            var payment = new Payment
            {
                BookingId = Guid.NewGuid(),
                Amount = 50.00m,
                Provider = "Stripe",
                CheckoutSessionId = checkoutSessionId,
                StatusId = pendingPaymentStatus.Id,
            };
            dbContext.Payments.Add(payment);
            await dbContext.SaveChangesAsync();

            var emailSenderMock = new Mock<IEmailSender>();
            var service = new PaymentsService(paymentRepo, paymentStatusRepo, bookingRepo, bookingStatusRepo, emailSenderMock.Object, new ConfigurationBuilder().Build());
            await service.CancelPaymentAsync(checkoutSessionId);

            var updatedPayment = dbContext.Payments.First(x => x.Id == payment.Id);
            Assert.Equal(cancelledPaymentStatus.Id, updatedPayment.StatusId);
        }

        [Fact]
        public async Task CancelPaymentAsyncShouldNotOverwriteAPaymentThatAlreadySucceeded()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;

            using var dbContext = new ApplicationDbContext(options);
            using var paymentRepo = new EfDeletableEntityRepository<Payment>(dbContext);
            using var paymentStatusRepo = new EfDeletableEntityRepository<PaymentStatus>(dbContext);
            using var bookingRepo = new EfDeletableEntityRepository<Booking>(dbContext);
            using var bookingStatusRepo = new EfDeletableEntityRepository<BookingStatus>(dbContext);

            var pendingPaymentStatus = new PaymentStatus { Id = Guid.NewGuid(), Name = "Pending" };
            var paidPaymentStatus = new PaymentStatus { Id = Guid.NewGuid(), Name = "DepositPaid" };
            var cancelledPaymentStatus = new PaymentStatus { Id = Guid.NewGuid(), Name = "Cancelled" };
            dbContext.PaymentStatuses.AddRange(pendingPaymentStatus, paidPaymentStatus, cancelledPaymentStatus);

            // The success webhook (or the Success redirect) already landed before the
            // expiry webhook arrived — a real race Stripe can produce.
            var checkoutSessionId = "cs_test_won_the_race";
            var payment = new Payment
            {
                BookingId = Guid.NewGuid(),
                Amount = 50.00m,
                Provider = "Stripe",
                CheckoutSessionId = checkoutSessionId,
                StatusId = paidPaymentStatus.Id,
                TransactionId = "txn_already_paid",
            };
            dbContext.Payments.Add(payment);
            await dbContext.SaveChangesAsync();

            var emailSenderMock = new Mock<IEmailSender>();
            var service = new PaymentsService(paymentRepo, paymentStatusRepo, bookingRepo, bookingStatusRepo, emailSenderMock.Object, new ConfigurationBuilder().Build());
            await service.CancelPaymentAsync(checkoutSessionId);

            var updatedPayment = dbContext.Payments.First(x => x.Id == payment.Id);
            Assert.Equal(paidPaymentStatus.Id, updatedPayment.StatusId);
        }

        [Fact]
        public async Task CancelPendingPaymentsForBookingsAsyncShouldOnlyCancelPendingPaymentsForGivenBookings()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;

            using var dbContext = new ApplicationDbContext(options);
            using var paymentRepo = new EfDeletableEntityRepository<Payment>(dbContext);
            using var paymentStatusRepo = new EfDeletableEntityRepository<PaymentStatus>(dbContext);
            using var bookingRepo = new EfDeletableEntityRepository<Booking>(dbContext);
            using var bookingStatusRepo = new EfDeletableEntityRepository<BookingStatus>(dbContext);

            var pendingPaymentStatus = new PaymentStatus { Id = Guid.NewGuid(), Name = "Pending" };
            var paidPaymentStatus = new PaymentStatus { Id = Guid.NewGuid(), Name = "DepositPaid" };
            var cancelledPaymentStatus = new PaymentStatus { Id = Guid.NewGuid(), Name = "Cancelled" };
            dbContext.PaymentStatuses.AddRange(pendingPaymentStatus, paidPaymentStatus, cancelledPaymentStatus);

            var staleBookingId = Guid.NewGuid();
            var otherStaleBookingId = Guid.NewGuid();
            var paidBookingId = Guid.NewGuid();
            var untouchedBookingId = Guid.NewGuid();

            var stalePayment = new Payment { BookingId = staleBookingId, Amount = 50.00m, Provider = "Stripe", CheckoutSessionId = "cs_stale", StatusId = pendingPaymentStatus.Id };
            var otherStalePayment = new Payment { BookingId = otherStaleBookingId, Amount = 50.00m, Provider = "Stripe", CheckoutSessionId = "cs_other_stale", StatusId = pendingPaymentStatus.Id };
            var paidPayment = new Payment { BookingId = paidBookingId, Amount = 50.00m, Provider = "Stripe", CheckoutSessionId = "cs_paid", StatusId = paidPaymentStatus.Id };
            var untouchedPayment = new Payment { BookingId = untouchedBookingId, Amount = 50.00m, Provider = "Stripe", CheckoutSessionId = "cs_untouched", StatusId = pendingPaymentStatus.Id };
            dbContext.Payments.AddRange(stalePayment, otherStalePayment, paidPayment, untouchedPayment);
            await dbContext.SaveChangesAsync();

            var emailSenderMock = new Mock<IEmailSender>();
            var service = new PaymentsService(paymentRepo, paymentStatusRepo, bookingRepo, bookingStatusRepo, emailSenderMock.Object, new ConfigurationBuilder().Build());

            // Only the stale bookings' pending payments should be cancelled; the paid
            // one must be left alone even though it's in the id list, and the pending
            // payment for a booking outside the list must be left alone too.
            await service.CancelPendingPaymentsForBookingsAsync(new[] { staleBookingId, otherStaleBookingId, paidBookingId });

            Assert.Equal(cancelledPaymentStatus.Id, dbContext.Payments.First(x => x.Id == stalePayment.Id).StatusId);
            Assert.Equal(cancelledPaymentStatus.Id, dbContext.Payments.First(x => x.Id == otherStalePayment.Id).StatusId);
            Assert.Equal(paidPaymentStatus.Id, dbContext.Payments.First(x => x.Id == paidPayment.Id).StatusId);
            Assert.Equal(pendingPaymentStatus.Id, dbContext.Payments.First(x => x.Id == untouchedPayment.Id).StatusId);
        }
    }
}
