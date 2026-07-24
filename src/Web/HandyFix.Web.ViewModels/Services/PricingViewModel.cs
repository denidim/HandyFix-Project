namespace HandyFix.Web.ViewModels.Services
{
    using System.Collections.Generic;

    public class PricingViewModel
    {
        public IEnumerable<CategoryViewModel> Categories { get; set; }

        public IEnumerable<ServiceViewModel> TypicalJobs { get; set; }
    }
}
