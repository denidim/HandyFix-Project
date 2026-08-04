namespace HandyFix.Web.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Services.Data.Bookings;
    using HandyFix.Services.Data.Payments;
    using HandyFix.Web.ViewModels.Booking;
    using HandyFix.Web.ViewModels.Payment;

    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Primitives;

    // Covers the Stripe webhook too (api/payment/webhook) - Stripe's server can't authenticate
    // as a logged-in HandyFix user, so this controller has to stay reachable anonymously as a
    // whole rather than trying to carve out just that one action.
    [AllowAnonymous]
    public class PaymentController : BaseController
    {
        private readonly IBookingsService bookingsService;
        private readonly IPaymentsService paymentsService;

        public PaymentController(
            IBookingsService bookingsService,
            IPaymentsService paymentsService)
        {
            this.bookingsService = bookingsService;
            this.paymentsService = paymentsService;
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

            PaymentCheckoutResult result = await this.paymentsService.CreateCheckoutSessionAsync(bookingId, booking.DepositAmount, successUrl, cancelUrl);

            return result.IsMock
                ? this.RedirectToAction("Success", new { session_id = result.SessionId })
                : this.Redirect(result.RedirectUrl);
        }

        [HttpGet]
        [Route("Payment/Success")]
        public async Task<IActionResult> Success(string session_id)
        {
            // Update status in case webhook isn't forwarded
            await this.paymentsService.ProcessPaymentSuccessAsync(session_id, $"txn_local_{Guid.NewGuid()}");

            IEnumerable<PaymentViewModel> payments = await this.paymentsService.GetAllPaymentsAsync<PaymentViewModel>();
            PaymentViewModel payment = payments.FirstOrDefault(x => x.CheckoutSessionId == session_id);

            if (payment != null)
            {
                return this.RedirectToAction("Confirmed", "Booking", new { id = payment.BookingId });
            }

            return this.RedirectToAction("Index", "Home");
        }

        [HttpGet]
        [Route("Payment/Cancel")]
        public IActionResult Cancel(Guid bookingId)
        {
            var model = new PaymentCancelViewModel { BookingId = bookingId };
            return this.View(model);
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
                return this.BadRequest($"Webhook Error: {ex.Message}");
            }
        }
    }
}
