namespace HandyFix.Web.ViewModels.Reviews
{
    using System.Collections.Generic;

    public class ReviewsListViewModel
    {
        public IEnumerable<ReviewViewModel> Reviews { get; set; }

        // Opens the Business Profile on Google Maps, where the reviews are read.
        public string GoogleReviewsUrl { get; set; }

        // Opens Google's form for writing a review.
        public string GoogleLeaveReviewUrl { get; set; }

        public bool ShowOnSiteReviews { get; set; }
    }
}
