namespace HandyFix.Web.ViewModels.Administration.Dashboard
{
    using System.Collections.Generic;

    public class IndexViewModel
    {
        // How far back the dashboard lists the emails that did not go. A week: long enough for a
        // look at the panel every few days to catch one, short enough that the list empties.
        public const int EmailsNotSentDays = 7;

        // How far back the "How customers heard about us" table counts unless "All time" is
        // picked. A month: enough jobs for the figures to mean something, recent enough to show a
        // change (Sections 3ej and 3et).
        public const int HeardAboutDays = 30;

        // The value of the table's "All time" choice in the address (?heardAbout=all).
        public const string HeardAboutAllTime = "all";

        public IEnumerable<EmailNotSentViewModel> EmailsNotSent { get; set; } = new List<EmailNotSentViewModel>();

        public int TotalBookingsCount { get; set; }

        public int PendingBookingsCount { get; set; }

        public int AwaitingTechnicianCount { get; set; }

        public int TotalEnquiriesCount { get; set; }

        public decimal TotalRevenue { get; set; }

        public IReadOnlyList<HeardAboutCountViewModel> HeardAbout { get; set; } = new List<HeardAboutCountViewModel>();

        // Whether the table counts every enquiry and job ever made rather than the last 30 days.
        public bool HeardAboutIsAllTime { get; set; }
    }
}
