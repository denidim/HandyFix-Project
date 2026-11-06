namespace HandyFix.Common
{
    // The names the database gives a payment's status, in one place. Constants for the same
    // reason as BookingStatusNames.
    public static class PaymentStatusNames
    {
        // A Stripe payment page opened and not paid yet.
        public const string Pending = "Pending";

        // The website deposit, paid through Stripe.
        public const string DepositPaid = "DepositPaid";

        // Seeded, but nothing sets it today.
        public const string Confirmed = "Confirmed";

        // Money the admin wrote on a job.
        public const string Completed = "Completed";

        // A payment page closed without being paid.
        public const string Cancelled = "Cancelled";

        // A website deposit that was sent back.
        public const string Refunded = "Refunded";
    }
}
