namespace HandyFix.Web.ViewModels.Administration.Dashboard
{
    public class IndexViewModel
    {
        public int SettingsCount { get; set; }

        public int TotalBookingsCount { get; set; }

        public int PendingBookingsCount { get; set; }

        public int TotalEnquiriesCount { get; set; }

        public int PendingReviewsCount { get; set; }

        public decimal TotalRevenue { get; set; }
    }
}
