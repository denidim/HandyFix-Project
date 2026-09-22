namespace HandyFix.Web.ViewComponents
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using HandyFix.Services.Data.ServiceAreas;
    using HandyFix.Web.ViewModels.ServiceAreas;

    using Microsoft.AspNetCore.Mvc;

    public class FooterServiceAreasViewComponent : ViewComponent
    {
        private readonly IServiceAreasService areasService;

        public FooterServiceAreasViewComponent(IServiceAreasService areasService)
        {
            this.areasService = areasService;
        }

        // Every area, not a sample: the footer is on every page, so it is the one place each
        // area page is linked from site-wide (local SEO).
        public async Task<IViewComponentResult> InvokeAsync()
        {
            IEnumerable<ServiceAreaViewModel> areas = await this.areasService.GetAllAsync<ServiceAreaViewModel>();
            return this.View(areas);
        }
    }
}
