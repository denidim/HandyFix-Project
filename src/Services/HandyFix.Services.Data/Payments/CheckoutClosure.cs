namespace HandyFix.Services.Data.Payments
{
    // What came of closing a booking's open payment pages at Stripe.
    public enum CheckoutClosure
    {
        // No page is open any more: nobody can pay for this booking now.
        Closed = 0,

        // A page turned out to be paid. The deposit is written on the booking and it is on.
        Paid = 1,

        // Stripe could not be reached, so a page may still be open.
        Unknown = 2,
    }
}
