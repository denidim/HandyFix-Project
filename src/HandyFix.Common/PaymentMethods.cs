namespace HandyFix.Common
{
    using System.Collections.Generic;

    // How a payment the admin writes on a job was made. The deposit paid on the website is not
    // one of these: it is always a card, through Stripe, and the site records it by itself.
    public static class PaymentMethods
    {
        public const string Card = "Card";

        public const string Cash = "Cash";

        public const string BankTransfer = "Bank transfer";

        public static readonly IReadOnlyList<string> All = new[] { Card, Cash, BankTransfer };
    }
}
