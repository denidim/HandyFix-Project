namespace HandyFix.Web.Tests.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using HandyFix.Services.Data.Bookings;
    using HandyFix.Services.Data.Payments;
    using HandyFix.Web.Controllers;
    using HandyFix.Web.ViewModels.Booking;
    using HandyFix.Web.ViewModels.Payment;

    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Configuration;

    using Moq;

    using Xunit;

    /// <summary>
    /// Covers the Stripe sandbox bypass, which is a security control rather than a convenience:
    /// it must fire only when a key is genuinely absent AND the app is in Development. Getting
    /// this wrong in the unsafe direction means production silently reporting fake-successful
    /// payments for bookings nobody paid for.
    /// </summary>
    public class PaymentControllerTests
    {
        private const string MockProvider = "Stripe-Mock";

        [Fact]
        public async Task PayShouldBypassStripeWhenTheKeyIsMissingInDevelopment()
        {
            var controller = BuildController(
                stripeSecretKey: null,
                environmentName: "Development",
                out var bookingsService,
                out var paymentsService);

            var bookingId = Guid.NewGuid();
            bookingsService
                .Setup(x => x.GetByIdAsync<BookingDetailsViewModel>(bookingId))
                .ReturnsAsync(new BookingDetailsViewModel { Id = bookingId, DepositAmount = 40m });

            var result = await controller.Pay(bookingId);

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Success", redirect.ActionName);

            // The mock session id has to reach the payment record, because Success()
            // looks the payment up by exactly that id on the way back.
            var sessionId = Assert.IsType<string>(redirect.RouteValues["session_id"]);
            Assert.StartsWith("mock_session_", sessionId);

            paymentsService.Verify(
                x => x.CreatePaymentRecordAsync(bookingId, 40m, MockProvider, sessionId),
                Times.Once);
        }

        [Theory]
        [InlineData("Production")]
        [InlineData("Staging")]
        [InlineData("QA")]
        public async Task PayShouldThrowRatherThanFakeAPaymentWhenTheKeyIsMissingOutsideDevelopment(string environmentName)
        {
            var controller = BuildController(
                stripeSecretKey: null,
                environmentName: environmentName,
                out var bookingsService,
                out var paymentsService);

            var bookingId = Guid.NewGuid();
            bookingsService
                .Setup(x => x.GetByIdAsync<BookingDetailsViewModel>(bookingId))
                .ReturnsAsync(new BookingDetailsViewModel { Id = bookingId, DepositAmount = 40m });

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.Pay(bookingId));
            Assert.Contains("Stripe is not configured", exception.Message);

            // The part that actually matters: failing loudly is only useful if it also
            // fails to record anything. A payment row here would mark the booking paid.
            paymentsService.Verify(
                x => x.CreatePaymentRecordAsync(It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>()),
                Times.Never);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public async Task PayShouldTreatABlankKeyAsMissingRatherThanConfigured(string blankKey)
        {
            // A key set to an empty string in appsettings is a misconfiguration, not a
            // configured key - outside Development it must fail the same way a null does.
            var controller = BuildController(
                stripeSecretKey: blankKey,
                environmentName: "Production",
                out var bookingsService,
                out var paymentsService);

            var bookingId = Guid.NewGuid();
            bookingsService
                .Setup(x => x.GetByIdAsync<BookingDetailsViewModel>(bookingId))
                .ReturnsAsync(new BookingDetailsViewModel { Id = bookingId, DepositAmount = 40m });

            await Assert.ThrowsAsync<InvalidOperationException>(() => controller.Pay(bookingId));

            paymentsService.Verify(
                x => x.CreatePaymentRecordAsync(It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>()),
                Times.Never);
        }

        [Fact]
        public async Task PayShouldReturnNotFoundForAnUnknownBookingBeforeTouchingStripe()
        {
            var controller = BuildController(
                stripeSecretKey: null,
                environmentName: "Production",
                out var bookingsService,
                out var paymentsService);

            bookingsService
                .Setup(x => x.GetByIdAsync<BookingDetailsViewModel>(It.IsAny<Guid>()))
                .ReturnsAsync((BookingDetailsViewModel)null);

            // Note this runs in Production with no key: reaching the Stripe branch would
            // throw. Getting NotFound proves the booking lookup guards it.
            var result = await controller.Pay(Guid.NewGuid());

            Assert.IsType<NotFoundResult>(result);
            paymentsService.Verify(
                x => x.CreatePaymentRecordAsync(It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>()),
                Times.Never);
        }

        [Fact]
        public async Task SuccessShouldProcessThePaymentAndRedirectToTheConfirmedBooking()
        {
            var controller = BuildController(null, "Development", out _, out var paymentsService);

            var bookingId = Guid.NewGuid();
            paymentsService
                .Setup(x => x.GetAllPaymentsAsync<PaymentViewModel>())
                .ReturnsAsync(new List<PaymentViewModel>
                {
                    new PaymentViewModel { CheckoutSessionId = "other_session", BookingId = Guid.NewGuid() },
                    new PaymentViewModel { CheckoutSessionId = "sess_123", BookingId = bookingId },
                });

            var result = await controller.Success("sess_123");

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Confirmed", redirect.ActionName);
            Assert.Equal("Booking", redirect.ControllerName);
            Assert.Equal(bookingId, redirect.RouteValues["id"]);

            // Called unconditionally so a booking still completes when the Stripe
            // webhook never arrives; the service itself is idempotent (see §1).
            paymentsService.Verify(
                x => x.ProcessPaymentSuccessAsync("sess_123", It.IsAny<string>()),
                Times.Once);
        }

        [Fact]
        public async Task SuccessShouldFallBackHomeWhenNoPaymentMatchesTheSession()
        {
            var controller = BuildController(null, "Development", out _, out var paymentsService);

            paymentsService
                .Setup(x => x.GetAllPaymentsAsync<PaymentViewModel>())
                .ReturnsAsync(new List<PaymentViewModel>());

            var result = await controller.Success("sess_unknown");

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Index", redirect.ActionName);
            Assert.Equal("Home", redirect.ControllerName);
        }

        [Fact]
        public void CancelShouldReturnTheViewCarryingTheBookingId()
        {
            var controller = BuildController(null, "Development", out _, out _);

            var bookingId = Guid.NewGuid();
            var result = controller.Cancel(bookingId);

            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<PaymentCancelViewModel>(viewResult.Model);
            Assert.Equal(bookingId, model.BookingId);
        }

        [Fact]
        public async Task WebhookShouldReturnBadRequestForAnUnverifiableSignature()
        {
            var controller = BuildController(null, "Development", out _, out var paymentsService);
            SetRequestBody(controller, "{\"id\":\"evt_forged\"}");

            var result = await controller.Webhook();

            // An unsigned or forged event must not be processed, and must not 500 -
            // Stripe retries on 5xx, so a crash here turns one bad request into many.
            Assert.IsType<BadRequestObjectResult>(result);
            paymentsService.Verify(
                x => x.ProcessPaymentSuccessAsync(It.IsAny<string>(), It.IsAny<string>()),
                Times.Never);
            paymentsService.Verify(x => x.CancelPaymentAsync(It.IsAny<string>()), Times.Never);
        }

        private static void SetRequestBody(PaymentController controller, string body)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(body);
            controller.ControllerContext.HttpContext.Request.Body = new System.IO.MemoryStream(bytes);
        }

        private static PaymentController BuildController(
            string stripeSecretKey,
            string environmentName,
            out Mock<IBookingsService> bookingsService,
            out Mock<IPaymentsService> paymentsService)
        {
            bookingsService = new Mock<IBookingsService>();
            paymentsService = new Mock<IPaymentsService>();

            var settings = new Dictionary<string, string>();
            if (stripeSecretKey != null)
            {
                settings["Stripe:SecretKey"] = stripeSecretKey;
            }

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(settings)
                .Build();

            var environment = new Mock<IWebHostEnvironment>();
            environment.SetupGet(x => x.EnvironmentName).Returns(environmentName);

            var controller = new PaymentController(
                bookingsService.Object,
                paymentsService.Object,
                configuration,
                environment.Object)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext(),
                },
            };

            return controller;
        }
    }
}
