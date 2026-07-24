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
    }
}
