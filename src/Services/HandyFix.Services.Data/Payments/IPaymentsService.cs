namespace HandyFix.Services.Data.Payments
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using HandyFix.Web.ViewModels.Payment;

    public interface IPaymentsService
    {
        Task<Guid> CreatePaymentRecordAsync(Guid bookingId, decimal amount, string provider, string checkoutSessionId);

        Task ProcessPaymentSuccessAsync(string checkoutSessionId, string transactionId);

        Task CancelPaymentAsync(string checkoutSessionId);

        Task CancelPendingPaymentsForBookingsAsync(IEnumerable<Guid> bookingIds);

        Task<IEnumerable<T>> GetPaymentsForBookingAsync<T>(Guid bookingId);

        Task<IEnumerable<T>> GetAllPaymentsAsync<T>();

        Task<decimal> GetTotalRevenueAsync();

        /// <summary>
        /// Writes a payment on a job: "Card", "Cash" or "Bank transfer". False, with nothing
        /// changed, when the job is missing, cancelled or abandoned, or the amount or the way it
        /// was paid is not one the site takes.
        /// </summary>
        Task<bool> AddPaymentAsync(Guid bookingId, decimal amount, string method);

        /// <summary>
        /// Takes a payment the admin wrote off the job again. The website deposit cannot be.
        /// </summary>
        Task<bool> RemovePaymentAsync(Guid bookingId, Guid paymentId);

        /// <summary>
        /// Ticks, or unticks, "deposit refunded" on a cancelled job.
        /// </summary>
        Task<bool> SetDepositRefundedAsync(Guid bookingId, bool refunded);

        /// <summary>
        /// A job's money list: every payment that came in, oldest first.
        /// </summary>
        Task<IEnumerable<T>> GetMoneyListAsync<T>(Guid bookingId);

        /// <summary>
        /// Owns the sandbox-bypass-vs-real-Stripe-Checkout decision entirely: sandbox is always
        /// allowed in Development, and outside it only via the explicit
        /// Stripe:AllowSandboxOutsideDevelopment opt-in (so staging can demo the booking flow
        /// before a real Stripe account exists, while production stays protected by default).
        /// Throws InvalidOperationException if the key is missing and sandbox isn't allowed -
        /// never silently fakes a payment or attempts a doomed Stripe call.
        /// </summary>
        Task<PaymentCheckoutResult> CreateCheckoutSessionAsync(Guid bookingId, decimal depositAmount, string successUrl, string cancelUrl);

        /// <summary>
        /// Verifies the webhook signature, constructs the Stripe event, and dispatches
        /// checkout.session.completed/expired to ProcessPaymentSuccessAsync/CancelPaymentAsync.
        /// Throws on an invalid signature or malformed payload - the caller is expected to turn
        /// that into a 400 rather than a 500, since a bad signature is an untrusted-caller
        /// problem, not a server fault.
        /// </summary>
        Task HandleWebhookEventAsync(string json, string signature);
    }
}
