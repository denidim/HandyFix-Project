namespace HandyFix.Web.ViewModels.Administration.Dashboard
{
    public class IndexViewModel
    {
        public int TotalBookingsCount { get; set; }

        public int PendingBookingsCount { get; set; }

        public int AwaitingTechnicianCount { get; set; }

        public int TotalEnquiriesCount { get; set; }

        public decimal TotalRevenue { get; set; }
    }
}
