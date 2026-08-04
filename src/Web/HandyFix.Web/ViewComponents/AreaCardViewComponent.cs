namespace HandyFix.Web.ViewComponents
{
    using System;
    using System.Collections.Generic;
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
            // Passing excludeAreaId means "areas near this one" - use the service's
            // drive-time-proximity ordering rather than the default featured/all list.
            if (excludeAreaId.HasValue)
            {
                IEnumerable<ServiceAreaViewModel> nearest = await this.areasService.GetNearestAsync<ServiceAreaViewModel>(excludeAreaId.Value, take);
                return this.View(nearest.ToList());
            }

            IEnumerable<ServiceAreaViewModel> areas = await this.areasService.GetAllAsync<ServiceAreaViewModel>();

            var filtered = areas
                .Where(a => !featuredOnly || a.IsFeatured)
                .Take(take)
                .ToList();

            return this.View(filtered);
        }
    }
}
