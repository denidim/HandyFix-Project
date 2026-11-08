namespace HandyFix.Web.ViewModels.Booking
{
    public enum TechnicianAssignmentOutcome
    {
        BookingNotFound = 0,

        TechnicianNotFound = 1,

        /// <summary>
        /// The booking's deposit is not paid, or the booking is completed, cancelled or abandoned.
        /// </summary>
        NotAllowed = 2,

        /// <summary>
        /// The technician asked for is the one the booking already had. No email goes out.
        /// </summary>
        Unchanged = 3,

        Cleared = 4,

        AssignedAndCustomerEmailed = 5,

        /// <summary>
        /// The technician is saved, and the email to the customer could not be sent.
        /// </summary>
        AssignedButEmailNotSent = 6,

        /// <summary>
        /// The technician is saved on a job with no email address, which only a job the admin
        /// wrote in can be. The admin tells the customer who is coming.
        /// </summary>
        AssignedNoEmailAddress = 7,
    }
}
