namespace HandyFix.Services.Data.Payments
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using HandyFix.Web.ViewModels.Payment;

    public interface IPaymentsService
    {
        Task<Guid> CreatePaymentRecordAsync(Guid bookingId, decimal amount, string provider, string checkoutSessionId);

        /// <summary>
        /// Asks Stripe whether a payment page was paid and, when it was, writes the deposit on
        /// its booking and sends the emails; once, however often it is called. Answers with the
        /// booking the page belongs to, paid or not, and with null for a page the site never
        /// opened. Both ways a payment is heard of come through here: the customer's return
        /// from Stripe and Stripe's own message.
        /// </summary>
        Task<Guid?> ConfirmCheckoutAsync(string checkoutSessionId);

        /// <summary>
        /// Closes every payment page still open for a booking, at Stripe, so that nobody can
        /// pay for it any more. Called before the booking is dropped or cancelled, and before a
        /// new page is opened for it.
        /// </summary>
        Task<CheckoutClosure> CloseCheckoutsAsync(Guid bookingId);

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
        /// Opens a payment page for a booking's deposit, closing any earlier one first. Null
        /// when there is nothing to pay: the booking is missing, already paid, dropped,
        /// cancelled, or was written in by the admin.
        /// Owns the sandbox-bypass-vs-real-Stripe-Checkout decision entirely: sandbox is always
        /// allowed in Development, and outside it only via the explicit
        /// Stripe:AllowSandboxOutsideDevelopment opt-in, which the live site must never have.
        /// Throws InvalidOperationException if the key is missing and sandbox isn't allowed -
        /// never silently fakes a payment or attempts a doomed Stripe call.
        /// </summary>
        Task<PaymentCheckoutResult> CreateCheckoutSessionAsync(Guid bookingId, string successUrl, string cancelUrl);

        /// <summary>
        /// Checks that Stripe signed the message, then passes checkout.session.completed to
        /// ConfirmCheckoutAsync and checkout.session.expired to CancelPaymentAsync.
        /// Throws on an invalid signature or malformed payload - the caller is expected to turn
        /// that into a 400 rather than a 500, since a bad signature is an untrusted-caller
        /// problem, not a server fault.
        /// </summary>
        Task HandleWebhookEventAsync(string json, string signature);
    }
}
