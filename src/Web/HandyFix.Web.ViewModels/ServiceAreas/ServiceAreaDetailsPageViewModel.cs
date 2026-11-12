namespace HandyFix.Web.ViewModels.ServiceAreas
{
    using System.Collections.Generic;

    using HandyFix.Web.ViewModels.Reviews;
    using HandyFix.Web.ViewModels.Services;

    public class ServiceAreaDetailsPageViewModel
    {
        public ServiceAreaDetailsViewModel Area { get; set; }

        public IEnumerable<ServiceViewModel> RelatedServices { get; set; }

        // This town's reviews only (Section 3eh).
        public IEnumerable<ReviewViewModel> Reviews { get; set; }

        /// <summary>
        /// The area's picture: the one the admin uploaded, else the one that came with the site;
        /// null when it has neither, and the page shows the general picture. Looked up before the
        /// page is drawn.
        /// </summary>
        public string Picture { get; set; }

        /// <summary>
        /// The strip of that picture a phone is sent, or null when there is none.
        /// </summary>
        public string PhonePicture { get; set; }
    }
}
