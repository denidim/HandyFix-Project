namespace HandyFix.Web.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Common;
    using HandyFix.Services;
    using HandyFix.Services.Data.Categories;
    using HandyFix.Services.Data.ServiceAreas;
    using HandyFix.Services.Data.Services;
    using HandyFix.Web.Presentation;
    using HandyFix.Web.ViewModels.ServiceAreas;
    using HandyFix.Web.ViewModels.Services;

    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;

    [AllowAnonymous]
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
        private readonly IServiceAreasService serviceAreasService;
        private readonly IImageStorageService imageStorage;

        public ServicesController(ICategoriesService categoriesService, IServicesService servicesService, IServiceAreasService serviceAreasService, IImageStorageService imageStorage)
        {
            this.categoriesService = categoriesService;
            this.servicesService = servicesService;
            this.serviceAreasService = serviceAreasService;
            this.imageStorage = imageStorage;
        }

        [Route("Services", Name = "ServicesList")]
        public async Task<IActionResult> Index()
        {
            IEnumerable<CategoryViewModel> categories = await this.categoriesService.GetAllAsync<CategoryViewModel>();

            // This page had no description until PROJECT_STATE.md Section 3dj, so search engines
            // made one up from whatever text came first.
            var rates = HourlyRates.Of(categories);
            this.SetPageMeta(
                "Plumbing & Handyman Services in Surrey",
                $"Every job we do in one list: plumbing at £{rates.Plumbing:F0} an hour, handyman work at £{rates.Handyman:F0}, and small building work at a fixed quote after a free survey. Book a slot online.",
                this.Url.Action("Index", "Services", null, this.Request.Scheme),
                PageTitle.BookOnline);
            return this.View(categories);
        }

        [Route("Pricing", Name = "Pricing")]
        public async Task<IActionResult> Pricing()
        {
            var categories = (await this.categoriesService.GetAllAsync<CategoryViewModel>()).ToList();

            // From the active services only. A category's own collection also holds the ones an
            // admin has switched off, which then stayed on this page with a Book Now button.
            IEnumerable<ServiceViewModel> allServices = await this.servicesService.GetAllAsync<ServiceViewModel>();
            IEnumerable<ServiceViewModel> typicalJobs = TypicalJobSlugs
                .Select(slug => allServices.FirstOrDefault(s => s.Slug == slug))
                .Where(s => s != null);

            IEnumerable<ServiceAreaViewModel> localAreas = await this.serviceAreasService.GetAllAsync<ServiceAreaViewModel>();

            var rates = HourlyRates.Of(categories);
            var model = new PricingViewModel
            {
                Categories = categories,
                TypicalJobs = typicalJobs.WithMediumCopies(this.imageStorage),
                LocalAreas = localAreas.Take(12),
            };

            this.SetPageMeta(
                $"Plumber £{rates.Plumbing:F0}, Handyman £{rates.Handyman:F0} an Hour in Surrey",
                $"Plumbing is £{rates.Plumbing:F0} an hour and handyman work £{rates.Handyman:F0}, with a one-hour minimum and no call-out fee. Small building work is quoted after a free survey. Book online.",
                this.Url.Action("Pricing", "Services", null, this.Request.Scheme),
                PageTitle.BookOnline);

            return this.View(model);
        }

        [Route("Services/{categorySlug}", Name = "ServiceCategory")]
        public async Task<IActionResult> Category(string categorySlug)
        {
            CategoryViewModel category = await this.categoriesService.GetBySlugAsync<CategoryViewModel>(categorySlug);
            if (category == null)
            {
                return this.NotFound();
            }

            IEnumerable<ServiceViewModel> services = await this.servicesService.GetByCategoryAsync<ServiceViewModel>(category.Name);
            category.Services = services.WithSmallCopies(this.imageStorage);

            // All areas: the coverage diagram plots every one; the view caps the badge links at 12.
            IEnumerable<ServiceAreaViewModel> localAreas = await this.serviceAreasService.GetAllAsync<ServiceAreaViewModel>();
            category.LocalAreas = localAreas;

            // The address is from the slug in the database, not the one in the address: routes
            // match whatever the letter case, and /services/PLUMBING has to name
            // /Services/plumbing as the page.
            var (title, titleEnding, description) = CategoryMeta(category);
            this.SetPageMeta(
                title,
                description,
                this.Url.RouteUrl("ServiceCategory", new { categorySlug = category.Slug }, this.Request.Scheme),
                titleEnding);

            return this.View(category);
        }

        [Route("Services/{categorySlug}/{serviceSlug}", Name = "ServiceDetails")]
        public async Task<IActionResult> Details(string categorySlug, string serviceSlug)
        {
            // A service an admin has switched off is "not visible to the public", as its form
            // says: no list shows it, and its own address answers 404 too.
            ServiceDetailsViewModel service = await this.servicesService.GetBySlugAsync<ServiceDetailsViewModel>(serviceSlug);
            if (service == null || !service.IsActive || !service.CategorySlug.Equals(categorySlug, StringComparison.OrdinalIgnoreCase))
            {
                return this.NotFound();
            }

            // The page's own questions, in the order the admin put them (Section 3ep).
            service.Faqs = (service.Faqs ?? Enumerable.Empty<ServiceFaqViewModel>())
                .OrderBy(f => f.DisplayOrder)
                .ToList();

            // "Worth doing in the same visit", where the page's text chose some. Those take the
            // place of the three cards of the same division at the foot of the page, so the page
            // does not offer two lists of other services.
            IEnumerable<ServiceViewModel> activeServices = await this.servicesService.GetAllAsync<ServiceViewModel>() ?? Enumerable.Empty<ServiceViewModel>();
            service.SameVisitServices = SameVisitServices.For(service.Slug, activeServices.Where(s => s.Id != service.Id));
            if (service.SameVisitServices.Any())
            {
                service.RelatedServices = Enumerable.Empty<ServiceViewModel>();
            }
            else
            {
                IEnumerable<ServiceViewModel> categoryServices = await this.servicesService.GetByCategoryAsync<ServiceViewModel>(service.CategoryName);
                service.RelatedServices = categoryServices
                    .Where(s => !s.Slug.Equals(service.Slug, StringComparison.OrdinalIgnoreCase))
                    .Take(3)
                    .WithMediumCopies(this.imageStorage);
            }

            // "Areas we cover": every town page, by name.
            IEnumerable<ServiceAreaViewModel> towns = await this.serviceAreasService.GetAllAsync<ServiceAreaViewModel>() ?? Enumerable.Empty<ServiceAreaViewModel>();
            service.Towns = towns.OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase).ToList();

            var isQuoteOnly = service.CategorySlug == CategorySlugs.SmallBuildingWorks || service.IsQuoteOnly;

            // The title (the SEO master plan's item 7): the service, the county and the price
            // people compare on, then what the visitor can do: book a slot online, or call for
            // an emergency, which is never booked as a slot. A quote-only job has no hourly
            // price, so its title offers the survey and nothing after it. The view's H1 stays
            // the service's name.
            var title = isQuoteOnly
                ? $"{service.Name} in Surrey, Free Survey"
                : $"{service.Name} in Surrey from £{service.BasePrice:F0}";
            var titleEnding = isQuoteOnly
                ? PageTitle.NoEnding
                : service.IsEmergency ? PageTitle.CallOrWhatsApp : PageTitle.BookOnline;

            // The description is the service's own summary, written for its page and its cards.
            // The formula that stood here was the same sentence on every page with the name put
            // in lower case ("Need tv mounting in Surrey"). A service without a summary gives the
            // start of its description, as its card does.
            this.SetPageMeta(
                title,
                Summaries.Of(service.Summary, service.Description),
                this.Url.RouteUrl("ServiceDetails", new { categorySlug = service.CategorySlug, serviceSlug = service.Slug }, this.Request.Scheme),
                titleEnding);

            return this.View(service);
        }

        // A category page's title, the title's ending and its description. The three divisions
        // each have their own accepted text; the rate in it is the division's own lowest price.
        // There is no admin page for categories, so the last line is only for a category the
        // code does not know yet.
        private static (string Title, string TitleEnding, string Description) CategoryMeta(CategoryViewModel category)
        {
            var rate = $"£{category.BasePrice:F0}";

            return category.Slug switch
            {
                CategorySlugs.Plumbing => (
                    $"Plumbing Services in Surrey from {rate} an Hour",
                    PageTitle.BookOnline,
                    $"Taps, toilets, leaks, radiators and showers repaired and fitted across Surrey and South London, at {rate} an hour with no call-out fee. Book a slot online."),
                CategorySlugs.Handyman => (
                    $"Handyman Services in Surrey from {rate} an Hour",
                    PageTitle.BookOnline,
                    $"Shelves, TVs, flat-pack furniture, doors, locks and small repairs across Surrey and South London, at {rate} an hour with no call-out fee. Book a slot online."),
                CategorySlugs.SmallBuildingWorks => (
                    "Small Building & Refurbishments in Surrey, Free Survey",
                    PageTitle.NoEnding,
                    "Bathrooms, kitchens, tiling, plastering, flooring and partition walls across Surrey and South London, at a fixed quote after a free survey of the job."),
                _ => (
                    $"{category.Name} Services",
                    PageTitle.BookOnline,
                    $"{category.Name} services across Surrey and South London. See what we do and book a slot online."),
            };
        }
    }
}
