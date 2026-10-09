namespace HandyFix.Common
{
    using System;

    public static class BookingReference
    {
        // The reference a booking goes by wherever a person reads it: the first eight characters
        // of its id, in capitals. The admin pages always showed it this way, while the emails and
        // the customer's confirmation page showed all 36 characters, so the reference a customer
        // read out over the phone was not the one on the admin's screen
        // (PROJECT_STATE.md Section 3ce).
        public static string Short(Guid bookingId)
        {
            return bookingId.ToString("N").Substring(0, 8).ToUpperInvariant();
        }
    }
}
