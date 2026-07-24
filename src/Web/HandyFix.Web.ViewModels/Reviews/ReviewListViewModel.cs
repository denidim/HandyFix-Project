namespace HandyFix.Web.ViewModels.Reviews
{
    using System.Collections.Generic;

    public class ReviewListViewModel
    {
        public IEnumerable<ReviewViewModel> Reviews { get; set; }

        public ReviewSortField SortField { get; set; }

        public bool Descending { get; set; }

        public string StatusFilter { get; set; }

        // Computed from the full, unfiltered review list so the summary stats keep
        // reflecting the whole business regardless of which status filter is applied
        // to the table below.
        public int PendingCount { get; set; }

        public double AverageRating { get; set; }

        public int TotalPublished { get; set; }

        public int ApprovalRate { get; set; }
    }
}
