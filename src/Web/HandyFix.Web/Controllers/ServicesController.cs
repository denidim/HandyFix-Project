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
        // Real, existing services closest in spirit to the "typical job" examples a
        // customer would picture, hand-picked rather than derived from a category-balance
        // rule (hence 1 Plumbing / 3 Handyman) - order here is the display order.
        private static readonly string[] TypicalJobSlugs =
        {
            "tap-repairs",
            "tv-mounting",
            "shelf-installation",
            "minor-electrical-tasks",
        };

        private readonly ICategoriesService categoriesService;
        private readonly IServicesService servicesService;

        public ServicesController(ICategoriesService categoriesService, IServicesService servicesService)
        {
            this.categoriesService = categoriesService;
            this.servicesService = servicesService;
        }

        [Route("Services", Name = "ServicesList")]
        public async Task<IActionResult> Index()
        {
            var categories = await this.categoriesService.GetAllAsync<CategoryViewModel>();
            return this.View(categories);
        }

        [Route("Pricing", Name = "Pricing")]
        public async Task<IActionResult> Pricing()
        {
            var categories = (await this.categoriesService.GetAllAsync<CategoryViewModel>()).ToList();

            var allServices = categories.SelectMany(c => c.Services);
            var typicalJobs = TypicalJobSlugs
                .Select(slug => allServices.FirstOrDefault(s => s.Slug == slug))
                .Where(s => s != null);

            var model = new PricingViewModel
            {
                Categories = categories,
                TypicalJobs = typicalJobs,
            };

            this.ViewData["Title"] = "Pricing - HandyFix South London";
            this.ViewData["MetaDescription"] = "Transparent, up-front hourly pricing for plumbing and handyman services across Sutton, Croydon, Epsom, and South London. No hidden fees.";

            return this.View(model);
        }

        [Route("Services/{categorySlug}", Name = "ServiceCategory")]
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

        [Route("Services/{categorySlug}/{serviceSlug}", Name = "ServiceDetails")]
        public async Task<IActionResult> Details(string categorySlug, string serviceSlug)
        {
            var service = await this.servicesService.GetBySlugAsync<ServiceDetailsViewModel>(serviceSlug);
            if (service == null || !service.CategorySlug.Equals(categorySlug, StringComparison.OrdinalIgnoreCase))
            {
                return this.NotFound();
            }

            var categoryServices = await this.servicesService.GetByCategoryAsync<ServiceViewModel>(service.CategoryName);
            service.RelatedServices = categoryServices
                .Where(s => !s.Slug.Equals(service.Slug, StringComparison.OrdinalIgnoreCase))
                .Take(3)
                .ToList();

            this.ViewData["Title"] = $"{service.Name} - HandyFix London";
            this.ViewData["MetaDescription"] = $"Need {service.Name.ToLower()} in South London? Certified plumbers and handymen, transparent pricing starting from £{service.BasePrice}. Book a slot now.";

            return this.View(service);
        }
    }
}
