namespace HandyFix.Web.ViewModels.Booking
{
    public enum JobCancelOutcome
    {
        /// <summary>
        /// The job is missing, no reason was given, or it is done, cancelled or abandoned
        /// already. Nothing was changed.
        /// </summary>
        NotAllowed = 0,

        /// <summary>
        /// A website booking waiting for its deposit turned out to be paid when its payment page
        /// was closed: the customer paid in the same moment. The booking is confirmed and the
        /// customer and the company have been emailed. Nothing was cancelled.
        /// </summary>
        PaidJustNow = 1,

        Cancelled = 2,
    }
}
