namespace HandyFix.Web.ViewModels.Reviews
{
    using System.Collections.Generic;

    public class ReviewsListViewModel
    {
        public IEnumerable<ReviewViewModel> Reviews { get; set; }

        public string GoogleReviewsUrl { get; set; }

        public bool ShowOnSiteReviews { get; set; }
    }
}
