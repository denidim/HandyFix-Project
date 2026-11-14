namespace HandyFix.Web.ViewModels.Services
{
    using System.Collections.Generic;

    using HandyFix.Web.ViewModels.ServiceAreas;

    public class PricingViewModel
    {
        public IEnumerable<CategoryViewModel> Categories { get; set; }

        // The two hourly rates the page's questions quote.
        public HourlyRates Rates { get; set; }

        public IEnumerable<ServiceViewModel> TypicalJobs { get; set; }

        public IEnumerable<ServiceAreaViewModel> LocalAreas { get; set; }
    }
}
