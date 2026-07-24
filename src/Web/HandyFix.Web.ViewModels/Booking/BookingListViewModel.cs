namespace HandyFix.Web.ViewModels.Booking
{
    using System.Collections.Generic;

    public class BookingListViewModel
    {
        public IEnumerable<BookingDetailsViewModel> Bookings { get; set; }

        public IEnumerable<string> StatusOptions { get; set; }

        public BookingSortField SortField { get; set; }

        public bool Descending { get; set; }

        public string StatusFilter { get; set; }

        // Computed from the full, unfiltered booking list so the summary cards keep
        // reflecting the whole business regardless of which status filter is applied
        // to the table below.
        public int TodaysAppointmentsCount { get; set; }

        public int PendingApprovalCount { get; set; }

        public decimal MonthlyRevenue { get; set; }
    }
}
