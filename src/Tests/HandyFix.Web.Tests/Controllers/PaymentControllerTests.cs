namespace HandyFix.Web.Tests.Controllers
{
    using System;
    using System.Threading.Tasks;

    using HandyFix.Data.Models;
    using HandyFix.Services.Data.Bookings;
    using HandyFix.Services.Data.Payments;
    using HandyFix.Web.Controllers;
    using HandyFix.Web.ViewModels.Booking;
    using HandyFix.Web.ViewModels.Payment;

    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Logging;

    using Moq;

    using Xunit;

    /// <summary>
    /// Controller-level wiring only. Whether a payment page was paid is asked of Stripe in
    /// PaymentsService.ConfirmCheckoutAsync, and the pretend payment (a security control, not
    /// a convenience) is decided in PaymentsService.CreateCheckoutSessionAsync; both are covered
    /// in StripePaymentsTests. This file checks that the controller sends each customer to the
    /// right page for where their booking stands, and turns a refused webhook into a 400.
    /// </summary>
    public class PaymentControllerTests
    {
        [Fact]
        public async Task PayShouldRedirectToSuccessWhenTheServiceReturnsAMockResult()
        {
            var controller = BuildController(out var bookingsService, out var paymentsService, out _);

            var bookingId = Guid.NewGuid();
            bookingsService
                .Setup(x => x.GetByIdAsync<BookingDetailsViewModel>(bookingId))
                .ReturnsAsync(WaitingForItsDeposit(bookingId));

            paymentsService
                .Setup(x => x.CreateCheckoutSessionAsync(bookingId, It.IsAny<string>(), It.IsAny<string>()))
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
            var controller = BuildController(out var bookingsService, out var paymentsService, out _);

            var bookingId = Guid.NewGuid();
            bookingsService
                .Setup(x => x.GetByIdAsync<BookingDetailsViewModel>(bookingId))
                .ReturnsAsync(WaitingForItsDeposit(bookingId));

            paymentsService
                .Setup(x => x.CreateCheckoutSessionAsync(bookingId, It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new PaymentCheckoutResult { IsMock = false, SessionId = "cs_real", RedirectUrl = "https://checkout.stripe.com/cs_real" });

            var result = await controller.Pay(bookingId);

            var redirect = Assert.IsType<RedirectResult>(result);
            Assert.Equal("https://checkout.stripe.com/cs_real", redirect.Url);
        }

        [Fact]
        public async Task PayShouldReturnNotFoundForAnUnknownBookingBeforeCallingTheService()
        {
            var controller = BuildController(out var bookingsService, out var paymentsService, out _);

            bookingsService
                .Setup(x => x.GetByIdAsync<BookingDetailsViewModel>(It.IsAny<Guid>()))
                .ReturnsAsync((BookingDetailsViewModel)null);

            var result = await controller.Pay(Guid.NewGuid());

            Assert.IsType<NotFoundResult>(result);
            paymentsService.Verify(
                x => x.CreateCheckoutSessionAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()),
                Times.Never);
        }

        // The address takes any booking's id. One that is paid, dropped or cancelled used to
        // get a new Stripe page all the same (PROJECT_STATE.md Section 3cg).
        [Theory]
        [InlineData("Approved", true)]
        [InlineData("Abandoned", false)]
        [InlineData("Cancelled", false)]
        public async Task PayShouldOpenNoPaymentPageForABookingWithNothingToPay(string status, bool depositPaid)
        {
            var controller = BuildController(out var bookingsService, out var paymentsService, out _);

            var bookingId = Guid.NewGuid();
            bookingsService
                .Setup(x => x.GetByIdAsync<BookingDetailsViewModel>(bookingId))
                .ReturnsAsync(new BookingDetailsViewModel { Id = bookingId, Source = BookingSource.Website, StatusName = status, IsDepositPaid = depositPaid });

            var result = await controller.Pay(bookingId);

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Cancel", redirect.ActionName);
            Assert.Equal(bookingId, redirect.RouteValues["bookingId"]);
            paymentsService.Verify(
                x => x.CreateCheckoutSessionAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()),
                Times.Never);
        }

        // The service looks at the booking again as it opens the page, and may find it paid or
        // dropped in the moment since the controller read it. It answers with nothing then.
        [Fact]
        public async Task PayShouldSendTheCustomerToThePaymentPageWhenTheServiceOpensNothing()
        {
            var controller = BuildController(out var bookingsService, out var paymentsService, out _);

            var bookingId = Guid.NewGuid();
            bookingsService
                .Setup(x => x.GetByIdAsync<BookingDetailsViewModel>(bookingId))
                .ReturnsAsync(WaitingForItsDeposit(bookingId));
            paymentsService
                .Setup(x => x.CreateCheckoutSessionAsync(bookingId, It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync((PaymentCheckoutResult)null);

            var result = await controller.Pay(bookingId);

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Cancel", redirect.ActionName);
        }

        [Fact]
        public async Task SuccessShouldAskTheServiceAndRedirectToTheBookingItNames()
        {
            var controller = BuildController(out _, out var paymentsService, out _);

            var bookingId = Guid.NewGuid();
            paymentsService.Setup(x => x.ConfirmCheckoutAsync("sess_123")).ReturnsAsync(bookingId);

            var result = await controller.Success("sess_123");

            // Paid or not: "Confirmed" shows a booking only when its deposit is in.
            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Confirmed", redirect.ActionName);
            Assert.Equal("Booking", redirect.ControllerName);
            Assert.Equal(bookingId, redirect.RouteValues["id"]);
        }

        [Fact]
        public async Task SuccessShouldFallBackHomeWhenNoPaymentMatchesTheSession()
        {
            var controller = BuildController(out _, out var paymentsService, out _);

            paymentsService.Setup(x => x.ConfirmCheckoutAsync("sess_unknown")).ReturnsAsync((Guid?)null);

            var result = await controller.Success("sess_unknown");

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Index", redirect.ActionName);
            Assert.Equal("Home", redirect.ControllerName);
        }

        [Theory]
        [InlineData("Pending", false, PaymentPageState.NotPaid)]
        [InlineData("Abandoned", false, PaymentPageState.NoLongerHeld)]
        [InlineData("Abandoned", true, PaymentPageState.DepositReceived)]
        [InlineData("Cancelled", false, PaymentPageState.Cancelled)]
        [InlineData("Cancelled", true, PaymentPageState.Cancelled)]
        public async Task CancelShouldTellTheCustomerWhereTheirBookingStands(string status, bool depositPaid, PaymentPageState expected)
        {
            var controller = BuildController(out var bookingsService, out _, out _);

            var bookingId = Guid.NewGuid();
            bookingsService
                .Setup(x => x.GetByIdAsync<BookingDetailsViewModel>(bookingId))
                .ReturnsAsync(new BookingDetailsViewModel { Id = bookingId, Source = BookingSource.Website, StatusName = status, IsDepositPaid = depositPaid });

            var result = await controller.Cancel(bookingId);

            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<PaymentCancelViewModel>(viewResult.Model);
            Assert.Equal(bookingId, model.BookingId);
            Assert.Equal(expected, model.State);
        }

        [Fact]
        public async Task CancelShouldSendAPaidBookingToItsConfirmation()
        {
            var controller = BuildController(out var bookingsService, out _, out _);

            var bookingId = Guid.NewGuid();
            bookingsService
                .Setup(x => x.GetByIdAsync<BookingDetailsViewModel>(bookingId))
                .ReturnsAsync(new BookingDetailsViewModel { Id = bookingId, Source = BookingSource.Website, StatusName = "Approved", IsDepositPaid = true });

            var result = await controller.Cancel(bookingId);

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Confirmed", redirect.ActionName);
            Assert.Equal("Booking", redirect.ControllerName);
            Assert.Equal(bookingId, redirect.RouteValues["id"]);
        }

        // No booking, an id the site does not know, or a job the admin wrote in: the page says
        // the payment was not made and offers nothing to retry.
        [Theory]
        [InlineData(false, false)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public async Task CancelShouldOfferNothingToRetryWithoutAWebsiteBooking(bool withId, bool writtenIn)
        {
            var controller = BuildController(out var bookingsService, out _, out _);

            bookingsService
                .Setup(x => x.GetByIdAsync<BookingDetailsViewModel>(It.IsAny<Guid>()))
                .ReturnsAsync(writtenIn ? new BookingDetailsViewModel { Source = BookingSource.Phone, StatusName = "Approved" } : null);

            var result = await controller.Cancel(withId ? Guid.NewGuid() : null);

            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Null(Assert.IsType<PaymentCancelViewModel>(viewResult.Model).BookingId);
        }

        [Fact]
        public async Task WebhookShouldReturnBadRequestAndLogWhenTheServiceRejectsTheEvent()
        {
            // An unsigned or forged event must not 500. The reason is not sent back to the
            // caller, who may be anyone; it goes to the log, which used to hear nothing of a
            // refused call, Stripe's own included (PROJECT_STATE.md Section 3cg).
            var controller = BuildController(out _, out var paymentsService, out var logger);
            SetRequestBody(controller, "{\"id\":\"evt_forged\"}");

            paymentsService
                .Setup(x => x.HandleWebhookEventAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(new InvalidOperationException("No signatures found matching the expected signature for payload."));

            var result = await controller.Webhook();

            Assert.IsType<BadRequestResult>(result);
            logger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<InvalidOperationException>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task WebhookShouldReturnOkWhenTheServiceAcceptsTheEvent()
        {
            var controller = BuildController(out _, out var paymentsService, out _);
            SetRequestBody(controller, "{\"id\":\"evt_real\"}");

            var result = await controller.Webhook();

            Assert.IsType<OkResult>(result);
            paymentsService.Verify(x => x.HandleWebhookEventAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        }

        private static BookingDetailsViewModel WaitingForItsDeposit(Guid bookingId) => new BookingDetailsViewModel
        {
            Id = bookingId,
            Source = BookingSource.Website,
            StatusName = "Pending",
            DepositAmount = 50m,
        };

        private static void SetRequestBody(PaymentController controller, string body)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(body);
            controller.ControllerContext.HttpContext.Request.Body = new System.IO.MemoryStream(bytes);
        }

        private static PaymentController BuildController(
            out Mock<IBookingsService> bookingsService,
            out Mock<IPaymentsService> paymentsService,
            out Mock<ILogger<PaymentController>> logger)
        {
            bookingsService = new Mock<IBookingsService>();
            paymentsService = new Mock<IPaymentsService>();
            logger = new Mock<ILogger<PaymentController>>();

            return new PaymentController(bookingsService.Object, paymentsService.Object, logger.Object)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext(),
                },
            };
        }
    }
}
