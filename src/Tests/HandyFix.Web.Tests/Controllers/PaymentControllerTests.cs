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

    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;

    using Moq;

    using Xunit;

    /// <summary>
    /// Controller-level wiring only. The Stripe sandbox-bypass decision (a security control, not
    /// a convenience - it must fire only when a key is genuinely absent AND the environment
    /// allows it) now lives entirely in PaymentsService.CreateCheckoutSessionAsync, covered in
    /// PaymentsServiceTests.CreateCheckoutSessionAsyncTests. This file only checks the controller
    /// correctly branches on the service's result and translates a webhook failure to a 400.
    /// </summary>
    public class PaymentControllerTests
    {
        [Fact]
        public async Task PayShouldRedirectToSuccessWhenTheServiceReturnsAMockResult()
        {
            var controller = BuildController(out var bookingsService, out var paymentsService);

            var bookingId = Guid.NewGuid();
            bookingsService
                .Setup(x => x.GetByIdAsync<BookingDetailsViewModel>(bookingId))
                .ReturnsAsync(new BookingDetailsViewModel { Id = bookingId, DepositAmount = 40m });

            paymentsService
                .Setup(x => x.CreateCheckoutSessionAsync(bookingId, 40m, It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new PaymentCheckoutResult { IsMock = true, SessionId = "mock_session_abc" });

            var result = await controller.Pay(bookingId);

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Success", redirect.ActionName);

            // The mock session id has to reach the redirect, because Success() looks the
            // payment up by exactly that id on the way back.
            Assert.Equal("mock_session_abc", redirect.RouteValues["session_id"]);
        }

        [Fact]
        public async Task PayShouldRedirectToTheRealCheckoutUrlWhenTheServiceReturnsARealSession()
        {
            var controller = BuildController(out var bookingsService, out var paymentsService);

            var bookingId = Guid.NewGuid();
            bookingsService
                .Setup(x => x.GetByIdAsync<BookingDetailsViewModel>(bookingId))
                .ReturnsAsync(new BookingDetailsViewModel { Id = bookingId, DepositAmount = 40m });

            paymentsService
                .Setup(x => x.CreateCheckoutSessionAsync(bookingId, 40m, It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new PaymentCheckoutResult { IsMock = false, SessionId = "cs_real", RedirectUrl = "https://checkout.stripe.com/cs_real" });

            var result = await controller.Pay(bookingId);

            var redirect = Assert.IsType<RedirectResult>(result);
            Assert.Equal("https://checkout.stripe.com/cs_real", redirect.Url);
        }

        [Fact]
        public async Task PayShouldReturnNotFoundForAnUnknownBookingBeforeCallingTheService()
        {
            var controller = BuildController(out var bookingsService, out var paymentsService);

            bookingsService
                .Setup(x => x.GetByIdAsync<BookingDetailsViewModel>(It.IsAny<Guid>()))
                .ReturnsAsync((BookingDetailsViewModel)null);

            var result = await controller.Pay(Guid.NewGuid());

            Assert.IsType<NotFoundResult>(result);
            paymentsService.Verify(
                x => x.CreateCheckoutSessionAsync(It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>()),
                Times.Never);
        }

        [Fact]
        public async Task SuccessShouldProcessThePaymentAndRedirectToTheConfirmedBooking()
        {
            var controller = BuildController(out _, out var paymentsService);

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
            var controller = BuildController(out _, out var paymentsService);

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
            var controller = BuildController(out _, out _);

            var bookingId = Guid.NewGuid();
            var result = controller.Cancel(bookingId);

            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<PaymentCancelViewModel>(viewResult.Model);
            Assert.Equal(bookingId, model.BookingId);
        }

        [Fact]
        public async Task WebhookShouldReturnBadRequestWhenTheServiceRejectsTheEvent()
        {
            // An unsigned or forged event must not 500 - Stripe retries on 5xx, so a crash
            // here turns one bad request into many. The signature-verification logic itself
            // lives in PaymentsService.HandleWebhookEventAsync now.
            var controller = BuildController(out _, out var paymentsService);
            SetRequestBody(controller, "{\"id\":\"evt_forged\"}");

            paymentsService
                .Setup(x => x.HandleWebhookEventAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(new InvalidOperationException("No signatures found matching the expected signature for payload."));

            var result = await controller.Webhook();

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task WebhookShouldReturnOkWhenTheServiceAcceptsTheEvent()
        {
            var controller = BuildController(out _, out var paymentsService);
            SetRequestBody(controller, "{\"id\":\"evt_real\"}");

            var result = await controller.Webhook();

            Assert.IsType<OkResult>(result);
            paymentsService.Verify(x => x.HandleWebhookEventAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        }

        private static void SetRequestBody(PaymentController controller, string body)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(body);
            controller.ControllerContext.HttpContext.Request.Body = new System.IO.MemoryStream(bytes);
        }

        private static PaymentController BuildController(
            out Mock<IBookingsService> bookingsService,
            out Mock<IPaymentsService> paymentsService)
        {
            bookingsService = new Mock<IBookingsService>();
            paymentsService = new Mock<IPaymentsService>();

            return new PaymentController(bookingsService.Object, paymentsService.Object)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext(),
                },
            };
        }
    }
}
