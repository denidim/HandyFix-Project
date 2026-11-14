namespace HandyFix.Web.ViewModels.Home
{
    using System.Collections.Generic;

    using HandyFix.Web.ViewModels.ServiceAreas;
    using HandyFix.Web.ViewModels.Services;

    // What the About page reads from the database, so that neither is typed into the page: the
    // two hourly rates its text quotes, and every town that has a page of its own.
    public class AboutViewModel
    {
        public HourlyRates Rates { get; set; }

        public IReadOnlyList<ServiceAreaViewModel> Towns { get; set; }
    }
}
