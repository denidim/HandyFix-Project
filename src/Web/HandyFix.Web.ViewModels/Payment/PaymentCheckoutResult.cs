namespace HandyFix.Web.ViewModels.Payment
{
    public class PaymentCheckoutResult
    {
        /// <summary>
        /// True when Stripe is bypassed (no key configured, sandbox allowed) and the deposit was
        /// recorded as already paid rather than a real checkout session being created.
        /// </summary>
        public bool IsMock { get; set; }

        public string SessionId { get; set; }

        /// <summary>
        /// Only set when IsMock is false - the real Stripe Checkout URL to redirect the customer
        /// to.
        /// </summary>
        public string RedirectUrl { get; set; }
    }
}
