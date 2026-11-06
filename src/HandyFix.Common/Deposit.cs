namespace HandyFix.Common
{
    using System.Globalization;

    // The deposit a website booking pays through Stripe, and the one place it is written. It is
    // a constant and not a setting on purpose: the Terms & Conditions name it, so changing it is
    // a release of its own, never a switch on a live server.
    //
    // The two texts are the amount without the pound sign. A page types the "£" itself and
    // writes one of these after it, the way the views print money: a "£" inside the value would
    // reach the page as "&#xA3;", since Razor encodes every character outside plain ASCII.
    public static class Deposit
    {
        public const decimal Amount = 50.00m;

        // "50", for the pages that say "a £50 deposit". The amount is a whole number of pounds.
        public static readonly string Pounds = Amount.ToString("0", CultureInfo.InvariantCulture);

        // "50.00", for the pages that say "£50.00".
        public static readonly string PoundsAndPence = Amount.ToString("F2", CultureInfo.InvariantCulture);
    }
}
