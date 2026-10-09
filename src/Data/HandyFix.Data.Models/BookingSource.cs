namespace HandyFix.Data.Models
{
    /// <summary>
    /// How a job reached the business. A job booked on the website pays a deposit and holds an
    /// hour in the calendar; every other kind is written in by the admin. Saved as its number,
    /// so the order here must not change: 0 is what every booking made before this existed is.
    /// </summary>
    public enum BookingSource
    {
        Website = 0,

        Phone = 1,

        WhatsApp = 2,

        Enquiry = 3,

        Agency = 4,

        Other = 5,
    }
}
