namespace HandyFix.Web.ViewModels.Booking
{
    using System;
    using System.ComponentModel.DataAnnotations;

    using HandyFix.Data.Models;

    /// <summary>
    /// A job's details put right after it was made: the name, the phone number, the email, the
    /// address, the service and what the job is. Its day and time are not here: "Move" changes
    /// those and looks after the calendar, and a second way to change the time would let the
    /// two disagree (PROJECT_STATE.md Section 3cf).
    /// </summary>
    public class JobEditInputModel : JobDetailsInputModel
    {
        public Guid Id { get; set; }

        /// <summary>
        /// Where a written-in job came from. A website booking's form has no such box: the site
        /// set it, and it stays "Website" whatever is sent.
        /// </summary>
        [Display(Name = "Came from")]
        public BookingSource? Source { get; set; }
    }
}
