namespace HandyFix.Services.Data.Payments
{
    using System.Threading.Tasks;

    using Stripe;
    using Stripe.Checkout;

    // Everything the site asks of Stripe goes through here and nothing else does, so the rules
    // around a payment can be tested with a stand-in in its place and no test calls Stripe
    // (PROJECT_STATE.md Section 3cg).
    public interface IStripeGateway
    {
        /// <summary>
        /// Opens a payment page for the customer.
        /// </summary>
        Task<Session> CreateCheckoutAsync(SessionCreateOptions options);

        /// <summary>
        /// How a payment page stands at Stripe now: open, paid or expired.
        /// </summary>
        Task<Session> GetCheckoutAsync(string sessionId);

        /// <summary>
        /// Closes a payment page so that nobody can pay on it any more, and answers with how it
        /// stands afterwards. A page that was paid in the meantime cannot be closed and comes
        /// back as paid.
        /// </summary>
        Task<Session> ExpireCheckoutAsync(string sessionId);

        /// <summary>
        /// Reads a message Stripe sent to the webhook. Throws when Stripe did not sign it.
        /// </summary>
        Event ReadEvent(string json, string signature, string webhookSecret);
    }
}
