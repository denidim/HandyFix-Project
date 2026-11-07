namespace HandyFix.Web.ViewComponents
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Services.Data.ServiceAreas;
    using HandyFix.Web.Presentation;
    using HandyFix.Web.ViewModels.ServiceAreas;

    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Mvc;

    public class AreaCardViewComponent : ViewComponent
    {
        private readonly IServiceAreasService areasService;
        private readonly IWebHostEnvironment environment;

        public AreaCardViewComponent(IServiceAreasService areasService, IWebHostEnvironment environment)
        {
            this.areasService = areasService;
            this.environment = environment;
        }

        public async Task<IViewComponentResult> InvokeAsync(
            Guid? excludeAreaId = null, int take = 6, bool featuredOnly = false)
        {
            // Passing excludeAreaId means "areas near this one" - use the service's
            // drive-time-proximity ordering rather than the default featured/all list.
            if (excludeAreaId.HasValue)
            {
                IEnumerable<ServiceAreaViewModel> nearest = await this.areasService.GetNearestAsync<ServiceAreaViewModel>(excludeAreaId.Value, take);
                return this.View(nearest.WithPictures(this.environment));
            }

            IEnumerable<ServiceAreaViewModel> areas = await this.areasService.GetAllAsync<ServiceAreaViewModel>();

            var filtered = areas
                .Where(a => !featuredOnly || a.IsFeatured)
                .Take(take)
                .WithPictures(this.environment);

            return this.View(filtered);
        }
    }
}
