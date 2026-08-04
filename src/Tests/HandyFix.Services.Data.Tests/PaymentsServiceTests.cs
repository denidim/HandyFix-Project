namespace HandyFix.Services.Data.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Data;
    using HandyFix.Data.Models;
    using HandyFix.Data.Repositories;
    using HandyFix.Services.Data.Payments;
    using HandyFix.Services.Messaging;
    using HandyFix.Web.ViewModels.Payment;

    using Microsoft.AspNetCore.Hosting;
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
            var service = new PaymentsService(paymentRepo, paymentStatusRepo, bookingRepo, bookingStatusRepo, emailSenderMock.Object, new ConfigurationBuilder().Build(), Mock.Of<IWebHostEnvironment>());
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
            var service = new PaymentsService(paymentRepo, paymentStatusRepo, bookingRepo, bookingStatusRepo, emailSenderMock.Object, configuration, Mock.Of<IWebHostEnvironment>());

            await service.ProcessPaymentSuccessAsync(checkoutSessionId, "txn_stripe_email_test");

            // The deposit confirmation names the service but deliberately not the technician -
            // assignment happens after payment, so this email would usually have nothing real to
            // say. This booking has a technician assigned precisely to prove it stays out.
            emailSenderMock.Verify(
                x => x.SendEmailAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    "john@example.com",
                    It.IsAny<string>(),
                    It.Is<string>(body => body.Contains("Leak Fix") && !body.Contains("Alex Smith")),
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
            var service = new PaymentsService(paymentRepo, paymentStatusRepo, bookingRepo, bookingStatusRepo, emailSenderMock.Object, new ConfigurationBuilder().Build(), Mock.Of<IWebHostEnvironment>());

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
            var service = new PaymentsService(paymentRepo, paymentStatusRepo, bookingRepo, bookingStatusRepo, emailSenderMock.Object, new ConfigurationBuilder().Build(), Mock.Of<IWebHostEnvironment>());

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
            var service = new PaymentsService(paymentRepo, paymentStatusRepo, bookingRepo, bookingStatusRepo, emailSenderMock.Object, new ConfigurationBuilder().Build(), Mock.Of<IWebHostEnvironment>());
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
            var service = new PaymentsService(paymentRepo, paymentStatusRepo, bookingRepo, bookingStatusRepo, emailSenderMock.Object, new ConfigurationBuilder().Build(), Mock.Of<IWebHostEnvironment>());
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
            var service = new PaymentsService(paymentRepo, paymentStatusRepo, bookingRepo, bookingStatusRepo, emailSenderMock.Object, new ConfigurationBuilder().Build(), Mock.Of<IWebHostEnvironment>());

            // Only the stale bookings' pending payments should be cancelled; the paid
            // one must be left alone even though it's in the id list, and the pending
            // payment for a booking outside the list must be left alone too.
            await service.CancelPendingPaymentsForBookingsAsync(new[] { staleBookingId, otherStaleBookingId, paidBookingId });

            Assert.Equal(cancelledPaymentStatus.Id, dbContext.Payments.First(x => x.Id == stalePayment.Id).StatusId);
            Assert.Equal(cancelledPaymentStatus.Id, dbContext.Payments.First(x => x.Id == otherStalePayment.Id).StatusId);
            Assert.Equal(paidPaymentStatus.Id, dbContext.Payments.First(x => x.Id == paidPayment.Id).StatusId);
            Assert.Equal(pendingPaymentStatus.Id, dbContext.Payments.First(x => x.Id == untouchedPayment.Id).StatusId);
        }

        /// <summary>
        /// Covers the Stripe sandbox bypass, which is a security control rather than a
        /// convenience: it must fire only when a key is genuinely absent AND the environment
        /// allows it. Getting this wrong in the unsafe direction means production silently
        /// reporting fake-successful payments for bookings nobody paid for.
        /// </summary>
        public class CreateCheckoutSessionAsyncTests
        {
            [Fact]
            public async Task ShouldBypassStripeWhenTheKeyIsMissingInDevelopment()
            {
                PaymentsService service = BuildService(stripeSecretKey: null, environmentName: "Development", out ApplicationDbContext dbContext);

                var bookingId = Guid.NewGuid();
                PaymentCheckoutResult result = await service.CreateCheckoutSessionAsync(bookingId, 40m, "https://example.com/success", "https://example.com/cancel");

                Assert.True(result.IsMock);
                Assert.StartsWith("mock_session_", result.SessionId);

                // The mock session id has to reach the payment record, because Success()
                // looks the payment up by exactly that id on the way back.
                Payment payment = dbContext.Payments.First(x => x.BookingId == bookingId);
                Assert.Equal("Stripe-Mock", payment.Provider);
                Assert.Equal(result.SessionId, payment.CheckoutSessionId);
            }

            [Theory]
            [InlineData("Production")]
            [InlineData("Staging")]
            [InlineData("QA")]
            public async Task ShouldThrowRatherThanFakeAPaymentWhenTheKeyIsMissingOutsideDevelopment(string environmentName)
            {
                PaymentsService service = BuildService(stripeSecretKey: null, environmentName: environmentName, out ApplicationDbContext dbContext);

                InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
                    () => service.CreateCheckoutSessionAsync(Guid.NewGuid(), 40m, "https://example.com/success", "https://example.com/cancel"));

                Assert.Contains("Stripe is not configured", exception.Message);

                // The part that actually matters: failing loudly is only useful if it also
                // fails to record anything. A payment row here would mark the booking paid.
                Assert.False(dbContext.Payments.Any());
            }

            [Theory]
            [InlineData("")]
            [InlineData("   ")]
            public async Task ShouldTreatABlankKeyAsMissingRatherThanConfigured(string blankKey)
            {
                // A key set to an empty string in appsettings is a misconfiguration, not a
                // configured key - outside Development it must fail the same way a null does.
                PaymentsService service = BuildService(stripeSecretKey: blankKey, environmentName: "Production", out ApplicationDbContext dbContext);

                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => service.CreateCheckoutSessionAsync(Guid.NewGuid(), 40m, "https://example.com/success", "https://example.com/cancel"));

                Assert.False(dbContext.Payments.Any());
            }

            private static PaymentsService BuildService(string stripeSecretKey, string environmentName, out ApplicationDbContext dbContext)
            {
                var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                    .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;

                dbContext = new ApplicationDbContext(options);
                var paymentRepo = new EfDeletableEntityRepository<Payment>(dbContext);
                var paymentStatusRepo = new EfDeletableEntityRepository<PaymentStatus>(dbContext);
                var bookingRepo = new EfDeletableEntityRepository<Booking>(dbContext);
                var bookingStatusRepo = new EfDeletableEntityRepository<BookingStatus>(dbContext);

                dbContext.PaymentStatuses.Add(new PaymentStatus { Name = "Pending" });
                dbContext.SaveChanges();

                var settings = new Dictionary<string, string>();
                if (stripeSecretKey != null)
                {
                    settings["Stripe:SecretKey"] = stripeSecretKey;
                }

                IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

                var environment = new Mock<IWebHostEnvironment>();
                environment.SetupGet(x => x.EnvironmentName).Returns(environmentName);

                return new PaymentsService(paymentRepo, paymentStatusRepo, bookingRepo, bookingStatusRepo, new Mock<IEmailSender>().Object, configuration, environment.Object);
            }
        }

        public class HandleWebhookEventAsyncTests
        {
            [Fact]
            public async Task ShouldThrowForAnUnverifiableSignature()
            {
                // An unsigned or forged event must not be processed - the caller (the webhook
                // controller action) turns this into a 400 rather than a 500, since Stripe
                // retries on 5xx and a crash here would turn one bad request into many.
                var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                    .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;

                using var dbContext = new ApplicationDbContext(options);
                using var paymentRepo = new EfDeletableEntityRepository<Payment>(dbContext);
                using var paymentStatusRepo = new EfDeletableEntityRepository<PaymentStatus>(dbContext);
                using var bookingRepo = new EfDeletableEntityRepository<Booking>(dbContext);
                using var bookingStatusRepo = new EfDeletableEntityRepository<BookingStatus>(dbContext);

                var service = new PaymentsService(paymentRepo, paymentStatusRepo, bookingRepo, bookingStatusRepo, new Mock<IEmailSender>().Object, new ConfigurationBuilder().Build(), Mock.Of<IWebHostEnvironment>());

                await Assert.ThrowsAnyAsync<Exception>(
                    () => service.HandleWebhookEventAsync("{\"id\":\"evt_forged\"}", string.Empty));
            }
        }
    }
}
