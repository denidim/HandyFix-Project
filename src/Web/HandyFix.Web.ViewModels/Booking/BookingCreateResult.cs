namespace HandyFix.Web.ViewModels.Booking
{
    using System;

    /// <summary>
    /// What came of a booking sent from the website's form, for the page the customer is shown
    /// next. A slot that is gone is an answer here, not an exception: the booking page puts it
    /// in its own words, and anything the service does throw is logged there.
    /// </summary>
    public class BookingCreateResult
    {
        public BookingCreateOutcome Outcome { get; set; }

        /// <summary>
        /// The new booking's id, when <see cref="Outcome"/> is <see cref="BookingCreateOutcome.Created"/>.
        /// </summary>
        public Guid BookingId { get; set; }
    }
}
