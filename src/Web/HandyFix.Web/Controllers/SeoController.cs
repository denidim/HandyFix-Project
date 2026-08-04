namespace HandyFix.Web.Controllers
{
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;

    using HandyFix.Services.Data.Categories;
    using HandyFix.Services.Data.ServiceAreas;
    using HandyFix.Services.Data.Services;
    using HandyFix.Web.Services;
    using HandyFix.Web.ViewModels.ServiceAreas;
    using HandyFix.Web.ViewModels.Services;

    using Microsoft.AspNetCore.Mvc;

    public class SeoController : BaseController
    {
        private readonly ICategoriesService categoriesService;
        private readonly IServicesService servicesService;
        private readonly IServiceAreasService areasService;

        public SeoController(ICategoriesService categoriesService, IServicesService servicesService, IServiceAreasService areasService)
        {
            this.categoriesService = categoriesService;
            this.servicesService = servicesService;
            this.areasService = areasService;
        }

        [Route("sitemap.xml")]
        public async Task<IActionResult> Sitemap()
        {
            var protocol = this.Request.Scheme;

            var urls = new List<string>
            {
                this.Url.Action("Index", "Home", null, protocol),
                this.Url.Action("Index", "Services", null, protocol),
                this.Url.Action("Pricing", "Services", null, protocol),
                this.Url.Action("Index", "Booking", null, protocol),
                this.Url.Action("Contact", "Home", null, protocol),
                this.Url.Action("About", "Home", null, protocol),
                this.Url.Action("FAQ", "Home", null, protocol),
                this.Url.RouteUrl("Areas", null, protocol),
                this.Url.Action("Reviews", "Home", null, protocol),
                this.Url.Action("Privacy", "Home", null, protocol),
                this.Url.Action("Terms", "Home", null, protocol),
                this.Url.Action("CookiePolicy", "Home", null, protocol),
            };

            IEnumerable<CategoryViewModel> categories = await this.categoriesService.GetAllAsync<CategoryViewModel>();
            foreach (CategoryViewModel category in categories)
            {
                urls.Add(this.Url.RouteUrl("ServiceCategory", new { categorySlug = category.Slug }, protocol));
            }

            IEnumerable<ServiceViewModel> services = await this.servicesService.GetAllAsync<ServiceViewModel>();
            foreach (ServiceViewModel service in services)
            {
                urls.Add(this.Url.RouteUrl("ServiceDetails", new { categorySlug = service.CategorySlug, serviceSlug = service.Slug }, protocol));
            }

            IEnumerable<ServiceAreaViewModel> areas = await this.areasService.GetAllAsync<ServiceAreaViewModel>();
            foreach (ServiceAreaViewModel area in areas)
            {
                urls.Add(this.Url.RouteUrl("AreaDetails", new { areaSlug = area.Slug }, protocol));
            }

            var content = SitemapXmlBuilder.Build(urls);

            return this.Content(content, "application/xml", Encoding.UTF8);
        }
    }
}
