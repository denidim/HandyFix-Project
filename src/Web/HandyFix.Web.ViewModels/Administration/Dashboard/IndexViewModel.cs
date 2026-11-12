namespace HandyFix.Web.ViewModels.Administration.Dashboard
{
    using System.Collections.Generic;

    public class IndexViewModel
    {
        // How far back the dashboard lists the emails that did not go. A week: long enough for a
        // look at the panel every few days to catch one, short enough that the list empties.
        public const int EmailsNotSentDays = 7;

        // How far back the "How customers heard about us" table counts. A month: enough jobs for
        // the figures to mean something, recent enough to show a change (Section 3ej).
        public const int HeardAboutDays = 30;

        public IEnumerable<EmailNotSentViewModel> EmailsNotSent { get; set; } = new List<EmailNotSentViewModel>();

        public int TotalBookingsCount { get; set; }

        public int PendingBookingsCount { get; set; }

        public int AwaitingTechnicianCount { get; set; }

        public int TotalEnquiriesCount { get; set; }

        public decimal TotalRevenue { get; set; }

        public IReadOnlyList<HeardAboutCountViewModel> HeardAbout { get; set; } = new List<HeardAboutCountViewModel>();
    }
}
