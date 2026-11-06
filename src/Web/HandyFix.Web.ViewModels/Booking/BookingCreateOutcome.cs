namespace HandyFix.Web.ViewModels.Booking
{
    public enum BookingCreateOutcome
    {
        /// <summary>
        /// The slot picked is not in the calendar: it was deleted while the form was open, or
        /// the form was sent with an id of its own. Nothing was saved.
        /// </summary>
        SlotNotFound = 0,

        /// <summary>
        /// Another booking took the slot, or the admin blocked it, between the customer picking
        /// it and sending the form. Nothing was saved.
        /// </summary>
        SlotTaken = 1,

        Created = 2,
    }
}
