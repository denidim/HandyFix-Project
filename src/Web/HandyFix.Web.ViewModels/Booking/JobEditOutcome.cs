namespace HandyFix.Web.ViewModels.Booking
{
    public enum JobEditOutcome
    {
        BookingNotFound = 0,

        /// <summary>
        /// The job is cancelled or abandoned: it keeps the details it ended with.
        /// </summary>
        NotAllowed = 1,

        /// <summary>
        /// A website booking was sent with no email. The site emails that customer, so the job
        /// keeps an address to email. Nothing was changed.
        /// </summary>
        EmailNeeded = 2,

        /// <summary>
        /// A written-in job was sent with nowhere it came from, or with "Website", which only
        /// the site itself sets. Nothing was changed.
        /// </summary>
        SourceNeeded = 3,

        /// <summary>
        /// The service picked is not on the list any more. Nothing was changed.
        /// </summary>
        ServiceNotFound = 4,

        /// <summary>
        /// The form was saved as it stood.
        /// </summary>
        Unchanged = 5,

        Saved = 6,
    }
}
