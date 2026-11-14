namespace HandyFix.Web.ViewModels.Booking
{
    // The bookings made on the website in a period, and how many of them had their deposit
    // paid: the last two steps of the Dashboard's count of what visitors did (PROJECT_STATE.md
    // Section 3fa).
    public class WebsiteBookingCounts
    {
        public int Sent { get; set; }

        public int DepositPaid { get; set; }
    }
}
