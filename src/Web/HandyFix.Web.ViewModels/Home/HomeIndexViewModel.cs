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

        /// <summary>
        /// Each card picture on the page with the address of its middle copy, or null when it has
        /// none: the three divisions' and the popular services'. The page used to look each one up
        /// on disk twice while it was drawn (PROJECT_STATE.md Section 3dj).
        /// </summary>
        public IReadOnlyDictionary<string, string> MediumCopies { get; set; } = new Dictionary<string, string>();
    }
}
