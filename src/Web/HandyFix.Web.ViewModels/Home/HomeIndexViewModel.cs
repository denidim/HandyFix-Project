namespace HandyFix.Web.ViewModels.Home
{
    using System;
    using System.Collections.Generic;

    using HandyFix.Web.ViewModels.Reviews;
    using HandyFix.Web.ViewModels.Services;

    public class HomeIndexViewModel
    {
        public IEnumerable<ServiceViewModel> Services { get; set; }

        public IEnumerable<ServiceViewModel> PopularServices { get; set; }

        public IEnumerable<CategoryViewModel> Categories { get; set; }

        public IEnumerable<ReviewViewModel> SliderReviews { get; set; }
    }
}
