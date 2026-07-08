namespace HandyFix.Web.ViewModels.Reviews
{
    using System.Collections.Generic;

    public class ReviewsListViewModel
    {
        public IEnumerable<ReviewViewModel> Reviews { get; set; }

        public ReviewInputModel NewReview { get; set; }
    }
}
