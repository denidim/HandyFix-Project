namespace HandyFix.Web.ViewModels.Booking
{
    public class TechnicianAssignmentResult
    {
        public TechnicianAssignmentOutcome Outcome { get; set; }

        /// <summary>
        /// The technician the booking has after the call, for the admin's message. Null when it
        /// has none.
        /// </summary>
        public string TechnicianName { get; set; }
    }
}
