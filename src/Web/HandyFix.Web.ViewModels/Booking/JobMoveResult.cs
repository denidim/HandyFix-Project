namespace HandyFix.Web.ViewModels.Booking
{
    using System;

    /// <summary>
    /// What moving a job did, for the line the admin is shown. A website booking carries its
    /// hour in the calendar with it; a job that was written in holds none.
    /// </summary>
    public class JobMoveResult
    {
        public JobMoveOutcome Outcome { get; set; }

        public DateTime NewStart { get; set; }

        /// <summary>
        /// The hour the job had held in the calendar went back on sale.
        /// </summary>
        public bool OldHourFreed { get; set; }

        /// <summary>
        /// The new hour was free in the calendar and is now this job's, off sale.
        /// </summary>
        public bool NewHourTaken { get; set; }

        /// <summary>
        /// A website booking moved to an hour the calendar could not give it: there is no slot
        /// at that time, or the slot is blocked or belongs to another booking.
        /// </summary>
        public bool NewHourNotAvailable { get; set; }
    }
}
