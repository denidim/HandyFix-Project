namespace HandyFix.Web.ViewComponents
{
    using System.Collections.Generic;
    using System.Linq;
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

        public async Task<IViewComponentResult> InvokeAsync(int take = 8)
        {
            IEnumerable<ServiceAreaViewModel> areas = await this.areasService.GetAllAsync<ServiceAreaViewModel>();
            return this.View(areas.Take(take).ToList());
        }
    }
}
