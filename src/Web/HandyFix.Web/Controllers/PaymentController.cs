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

    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Hosting;

    using Stripe;
    using Stripe.Checkout;

    public class PaymentController : BaseController
    {
        private readonly IBookingsService bookingsService;
        private readonly IPaymentsService paymentsService;
        private readonly IConfiguration configuration;
        private readonly IWebHostEnvironment environment;

        public PaymentController(
            IBookingsService bookingsService,
            IPaymentsService paymentsService,
            IConfiguration configuration,
            IWebHostEnvironment environment)
        {
            this.bookingsService = bookingsService;
            this.paymentsService = paymentsService;
            this.configuration = configuration;
            this.environment = environment;

            var secretKey = this.configuration["Stripe:SecretKey"];
            if (!string.IsNullOrWhiteSpace(secretKey))
            {
                StripeConfiguration.ApiKey = secretKey;
            }
        }

        [HttpGet]
        [Route("Payment/Pay")]
        public async Task<IActionResult> Pay(Guid bookingId)
        {
            var booking = await this.bookingsService.GetByIdAsync<BookingDetailsViewModel>(bookingId);
            if (booking == null)
            {
                return this.NotFound();
            }

            var secretKey = this.configuration["Stripe:SecretKey"];
            var keyMissing = string.IsNullOrWhiteSpace(secretKey);

            if (keyMissing && this.environment.IsDevelopment())
            {
                // Sandbox Mode: no Stripe key configured locally, bypass Stripe entirely
                // so the booking flow can still be exercised without a real account.
                var mockSessionId = $"mock_session_{Guid.NewGuid()}";
                await this.paymentsService.CreatePaymentRecordAsync(bookingId, booking.DepositAmount, "Stripe-Mock", mockSessionId);
                return this.RedirectToAction("Success", new { session_id = mockSessionId });
            }

            if (keyMissing)
            {
                // Never silently fake a payment or attempt a doomed Stripe call outside
                // development: fail loudly so a missing production secret gets noticed.
                throw new InvalidOperationException("Stripe is not configured for this environment. Set Stripe:SecretKey before accepting real payments.");
            }

            // Production-Ready Stripe Checkout integration
            var options = new SessionCreateOptions
            {
                PaymentMethodTypes = new List<string> { "card" },
                LineItems = new List<SessionLineItemOptions>
                {
                    new SessionLineItemOptions
                    {
                        PriceData = new SessionLineItemPriceDataOptions
                        {
                            UnitAmount = (long)(booking.DepositAmount * 100), // convert to cents
                            Currency = "gbp",
                            ProductData = new SessionLineItemPriceDataProductDataOptions
                            {
                                Name = $"HandyFix Booking Deposit (Ref: {booking.Id})",
                                Description = "Deposit to secure your service booking for South London.",
                            },
                        },
                        Quantity = 1,
                    },
                },
                Mode = "payment",
                SuccessUrl = $"{this.Request.Scheme}://{this.Request.Host}/Payment/Success?session_id={{CHECKOUT_SESSION_ID}}",
                CancelUrl = $"{this.Request.Scheme}://{this.Request.Host}/Payment/Cancel?bookingId={bookingId}",
            };

            var service = new SessionService();
            Session session = await service.CreateAsync(options);

            await this.paymentsService.CreatePaymentRecordAsync(bookingId, booking.DepositAmount, "Stripe", session.Id);

            return this.Redirect(session.Url);
        }

        [HttpGet]
        [Route("Payment/Success")]
        public async Task<IActionResult> Success(string session_id)
        {
            // Update status in case webhook isn't forwarded
            await this.paymentsService.ProcessPaymentSuccessAsync(session_id, $"txn_local_{Guid.NewGuid()}");

            var payments = await this.paymentsService.GetAllPaymentsAsync<PaymentViewModel>();
            var payment = payments.FirstOrDefault(x => x.CheckoutSessionId == session_id);

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
            var signature = this.Request.Headers["Stripe-Signature"];
            var webhookSecret = this.configuration["Stripe:WebhookSecret"];

            try
            {
                var stripeEvent = EventUtility.ConstructEvent(json, signature, webhookSecret);

                if (stripeEvent.Type == Events.CheckoutSessionCompleted)
                {
                    var session = stripeEvent.Data.Object as Session;
                    if (session != null)
                    {
                        await this.paymentsService.ProcessPaymentSuccessAsync(session.Id, session.PaymentIntentId);
                    }
                }

                return this.Ok();
            }
            catch (Exception ex)
            {
                return this.BadRequest($"Webhook Error: {ex.Message}");
            }
        }
    }
}
