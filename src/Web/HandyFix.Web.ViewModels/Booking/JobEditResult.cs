namespace HandyFix.Web.ViewModels.Booking
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// What saving a job's details did, for the line the admin is shown.
    /// </summary>
    public class JobEditResult
    {
        public JobEditOutcome Outcome { get; set; }

        /// <summary>
        /// The boxes that changed, in words ("phone number", "address"). Empty unless the
        /// outcome is <see cref="JobEditOutcome.Saved"/>.
        /// </summary>
        public IReadOnlyList<string> Changed { get; set; } = Array.Empty<string>();

        /// <summary>
        /// A website booking's email was changed. Whatever the site emailed that customer before
        /// went to the old address, and saving the new one sends nothing.
        /// </summary>
        public bool EmailChangedOnWebsiteBooking { get; set; }

        /// <summary>
        /// Set with <see cref="EmailChangedOnWebsiteBooking"/> when a technician is already on
        /// the job: the email naming them went to the old address.
        /// </summary>
        public string TechnicianName { get; set; }
    }
}
