namespace HandyFix.Web.Controllers
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Services.Data.Categories;
    using HandyFix.Services.Data.Services;
    using HandyFix.Web.ViewModels.Services;

    using Microsoft.AspNetCore.Mvc;

    public class ServicesController : BaseController
    {
        private readonly ICategoriesService categoriesService;
        private readonly IServicesService servicesService;

        public ServicesController(ICategoriesService categoriesService, IServicesService servicesService)
        {
            this.categoriesService = categoriesService;
            this.servicesService = servicesService;
        }

        [Route("Services")]
        public async Task<IActionResult> Index()
        {
            var categories = await this.categoriesService.GetAllAsync<CategoryViewModel>();
            return this.View(categories);
        }

        [Route("Services/{categorySlug}")]
        public async Task<IActionResult> Category(string categorySlug)
        {
            var category = await this.categoriesService.GetBySlugAsync<CategoryViewModel>(categorySlug);
            if (category == null)
            {
                return this.NotFound();
            }

            var services = await this.servicesService.GetByCategoryAsync<ServiceViewModel>(category.Name);
            category.Services = services.ToList();

            this.ViewData["Title"] = $"{category.Name} Services in South London";
            this.ViewData["MetaDescription"] = $"Professional {category.Name.ToLower()} services operating in Sutton, Croydon, Epsom, Kingston, Bromley, and across South London. Book your service online.";

            return this.View(category);
        }

        [Route("Services/{categorySlug}/{serviceSlug}")]
        public async Task<IActionResult> Details(string categorySlug, string serviceSlug)
        {
            var service = await this.servicesService.GetBySlugAsync<ServiceDetailsViewModel>(serviceSlug);
            if (service == null || !service.CategorySlug.Equals(categorySlug, StringComparison.OrdinalIgnoreCase))
            {
                return this.NotFound();
            }

            this.ViewData["Title"] = $"{service.Name} - HandyFix London";
            this.ViewData["MetaDescription"] = $"Need {service.Name.ToLower()} in South London? Certified plumbers and handymen, transparent pricing starting from £{service.BasePrice}. Book a slot now.";

            return this.View(service);
        }
    }
}
