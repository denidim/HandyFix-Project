namespace HandyFix.Web.ViewModels.ServiceAreas
{
    using System.Collections.Generic;

    using HandyFix.Web.ViewModels.Reviews;
    using HandyFix.Web.ViewModels.Services;

    public class ServiceAreaDetailsPageViewModel
    {
        public ServiceAreaDetailsViewModel Area { get; set; }

        public IEnumerable<ServiceViewModel> RelatedServices { get; set; }

        public IEnumerable<ReviewViewModel> Reviews { get; set; }

        public bool ShowOnSiteReviews { get; set; }

        /// <summary>
        /// The strip of the area's picture a phone is sent, or null when the area has none (one
        /// added in the admin panel). Looked up before the page is drawn.
        /// </summary>
        public string PhonePicture { get; set; }
    }
}
