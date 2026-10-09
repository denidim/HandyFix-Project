namespace HandyFix.Web.Controllers
{
    using System;
    using System.IO;
    using System.Threading.Tasks;

    using HandyFix.Services.Data.Bookings;
    using HandyFix.Services.Data.Payments;
    using HandyFix.Web.ViewModels.Booking;
    using HandyFix.Web.ViewModels.Payment;

    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Primitives;

    // Covers the Stripe webhook too (api/payment/webhook) - Stripe's server can't authenticate
    // as a logged-in HandyFix user, so this controller has to stay reachable anonymously as a
    // whole rather than trying to carve out just that one action.
    [AllowAnonymous]
    public class PaymentController : BaseController
    {
        private readonly IBookingsService bookingsService;
        private readonly IPaymentsService paymentsService;
        private readonly ILogger<PaymentController> logger;

        public PaymentController(
            IBookingsService bookingsService,
            IPaymentsService paymentsService,
            ILogger<PaymentController> logger)
        {
            this.bookingsService = bookingsService;
            this.paymentsService = paymentsService;
            this.logger = logger;
        }

        [HttpGet]
        [Route("Payment/Pay")]
        public async Task<IActionResult> Pay(Guid bookingId)
        {
            BookingDetailsViewModel booking = await this.bookingsService.GetByIdAsync<BookingDetailsViewModel>(bookingId);
            if (booking == null)
            {
                return this.NotFound();
            }

            var successUrl = $"{this.Request.Scheme}://{this.Request.Host}/Payment/Success?session_id={{CHECKOUT_SESSION_ID}}";
            var cancelUrl = $"{this.Request.Scheme}://{this.Request.Host}/Payment/Cancel?bookingId={bookingId}";

            PaymentCheckoutResult result = booking.CanPayDeposit
                ? await this.paymentsService.CreateCheckoutSessionAsync(bookingId, successUrl, cancelUrl)
                : null;

            if (result == null)
            {
                // There is nothing to pay: the booking is paid already, or is no longer held.
                // Cancel is the one place that works out which, and what the customer is told.
                return this.RedirectToAction("Cancel", new { bookingId });
            }

            return result.IsMock
                ? this.RedirectToAction("Success", new { session_id = result.SessionId })
                : this.Redirect(result.RedirectUrl);
        }

        // Where Stripe sends the customer back to. That they arrived here proves nothing: the
        // address can be typed by hand. The service asks Stripe whether the page was paid
        // (PROJECT_STATE.md Section 3cg), and "Confirmed" shows a booking only when it was.
        [HttpGet]
        [Route("Payment/Success")]
        public async Task<IActionResult> Success(string session_id)
        {
            Guid? bookingId = await this.paymentsService.ConfirmCheckoutAsync(session_id);

            return bookingId.HasValue
                ? this.RedirectToAction("Confirmed", "Booking", new { id = bookingId.Value })
                : this.RedirectToAction("Index", "Home");
        }

        // Every customer without a paid booking to show ends up here, from Stripe's "back"
        // link, from Pay and from Confirmed, and is told where their booking stands.
        [HttpGet]
        [Route("Payment/Cancel")]
        public async Task<IActionResult> Cancel(Guid? bookingId)
        {
            BookingDetailsViewModel booking = bookingId.HasValue
                ? await this.bookingsService.GetByIdAsync<BookingDetailsViewModel>(bookingId.Value)
                : null;

            if (booking == null || !booking.CameFromWebsite)
            {
                return this.View(new PaymentCancelViewModel());
            }

            if (booking.IsConfirmed)
            {
                return this.RedirectToAction("Confirmed", "Booking", new { id = booking.Id });
            }

            PaymentPageState state = PaymentPageState.NoLongerHeld;
            if (booking.CanPayDeposit)
            {
                state = PaymentPageState.NotPaid;
            }
            else if (booking.StatusName == "Cancelled")
            {
                state = PaymentPageState.Cancelled;
            }
            else if (booking.IsDepositPaid)
            {
                state = PaymentPageState.DepositReceived;
            }

            return this.View(new PaymentCancelViewModel { BookingId = booking.Id, State = state });
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        [Route("api/payment/webhook")]
        public async Task<IActionResult> Webhook()
        {
            var json = await new StreamReader(this.HttpContext.Request.Body).ReadToEndAsync();
            StringValues signature = this.Request.Headers["Stripe-Signature"];

            try
            {
                await this.paymentsService.HandleWebhookEventAsync(json, signature);
                return this.Ok();
            }
            catch (Exception ex)
            {
                // Anyone can post to this address, so a refusal is not an error of the site's.
                // It is written down all the same: when it is Stripe that was refused, a wrong
                // webhook secret say, this line is the only sign that paid deposits are not
                // being heard of. The caller is told nothing about why.
                this.logger.LogWarning(ex, "A call to the Stripe webhook was refused");
                return this.BadRequest();
            }
        }
    }
}
