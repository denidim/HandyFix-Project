namespace HandyFix.Web.ViewModels.Booking
{
    public enum JobMoveOutcome
    {
        BookingNotFound = 0,

        /// <summary>
        /// The job is done, cancelled or abandoned, or is a website booking still waiting for
        /// its deposit.
        /// </summary>
        NotAllowed = 1,

        /// <summary>
        /// The day and hour asked for are the ones the job already has.
        /// </summary>
        Unchanged = 2,

        /// <summary>
        /// The calendar changed under the move (another booking took the hour in the same
        /// moment). Nothing was changed; trying again is safe.
        /// </summary>
        CalendarChanged = 3,

        Moved = 4,
    }
}
