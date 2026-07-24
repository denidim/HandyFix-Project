namespace HandyFix.Web.ViewComponents
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Services.Data.ServiceAreas;
    using HandyFix.Web.ViewModels.ServiceAreas;

    using Microsoft.AspNetCore.Mvc;

    public class AreaCardViewComponent : ViewComponent
    {
        private readonly IServiceAreasService areasService;

        public AreaCardViewComponent(IServiceAreasService areasService)
        {
            this.areasService = areasService;
        }

        public async Task<IViewComponentResult> InvokeAsync(
            Guid? excludeAreaId = null, int take = 6, bool featuredOnly = false)
        {
            var areas = await this.areasService.GetAllAsync<ServiceAreaViewModel>();

            var filtered = areas
                .Where(a => excludeAreaId == null || a.Id != excludeAreaId)
                .Where(a => !featuredOnly || a.IsFeatured)
                .Take(take)
                .ToList();

            return this.View(filtered);
        }
    }
}
