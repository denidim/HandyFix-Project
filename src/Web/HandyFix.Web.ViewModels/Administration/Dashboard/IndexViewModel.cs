namespace HandyFix.Web.ViewModels.Administration.Dashboard
{
    using System.Collections.Generic;

    public class IndexViewModel
    {
        // How far back the dashboard lists the emails that did not go. A week: long enough for a
        // look at the panel every few days to catch one, short enough that the list empties.
        public const int EmailsNotSentDays = 7;

        public IEnumerable<EmailNotSentViewModel> EmailsNotSent { get; set; } = new List<EmailNotSentViewModel>();

        public int TotalBookingsCount { get; set; }

        public int PendingBookingsCount { get; set; }

        public int AwaitingTechnicianCount { get; set; }

        public int TotalEnquiriesCount { get; set; }

        public decimal TotalRevenue { get; set; }
    }
}
