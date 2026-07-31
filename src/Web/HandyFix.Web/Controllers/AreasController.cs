namespace HandyFix.Web.Controllers
{
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Services.Data.Reviews;
    using HandyFix.Services.Data.ServiceAreas;
    using HandyFix.Services.Data.Services;
    using HandyFix.Web.ViewModels.Reviews;
    using HandyFix.Web.ViewModels.ServiceAreas;
    using HandyFix.Web.ViewModels.Services;

    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Configuration;

    public class AreasController : BaseController
    {
        private readonly IServiceAreasService areasService;
        private readonly IServicesService servicesService;
        private readonly IReviewsService reviewsService;
        private readonly IConfiguration configuration;

        public AreasController(
            IServiceAreasService areasService,
            IServicesService servicesService,
            IReviewsService reviewsService,
            IConfiguration configuration)
        {
            this.areasService = areasService;
            this.servicesService = servicesService;
            this.reviewsService = reviewsService;
            this.configuration = configuration;
        }

        [Route("Areas", Name = "Areas")]
        public async Task<IActionResult> Index()
        {
            var areas = await this.areasService.GetAllAsync<ServiceAreaViewModel>();

            this.ViewData["Title"] = "Our Areas - HandyFix Coverage Across Surrey & South London";
            this.ViewData["MetaDescription"] = "See every town and village HandyFix covers across South London and Surrey, from Chessington and Kingston out to Guildford and Cobham. Find your area and book online.";
            this.ViewData["Canonical"] = this.Url.RouteUrl("Areas", null, this.Request.Scheme);

            return this.View(areas);
        }

        [Route("Areas/{areaSlug}", Name = "AreaDetails")]
        public async Task<IActionResult> Details(string areaSlug)
        {
            var area = await this.areasService.GetBySlugAsync<ServiceAreaDetailsViewModel>(areaSlug);
            if (area == null)
            {
                return this.NotFound();
            }

            // Guaranteed FAQ ordering - see ServiceAreaDetailsViewModel for why this
            // isn't done via a custom Mapster collection mapping.
            area.Faqs = area.Faqs.OrderBy(x => x.DisplayOrder).ToList();

            var plumbingServices = await this.servicesService.GetByCategoryAsync<ServiceViewModel>("Plumbing");
            var handymanServices = await this.servicesService.GetByCategoryAsync<ServiceViewModel>("Handyman");
            var relatedServices = plumbingServices.Take(2).Concat(handymanServices.Take(2)).ToList();

            // Hidden on-site until the Google Business Profile import ships, see
            // Business:ShowOnSiteReviews.
            var showOnSiteReviews = this.configuration.GetValue<bool>("Business:ShowOnSiteReviews");
            var reviews = showOnSiteReviews
                ? await this.reviewsService.GetLatestApprovedAsync<ReviewViewModel>(3)
                : Enumerable.Empty<ReviewViewModel>();

            var model = new ServiceAreaDetailsPageViewModel
            {
                Area = area,
                RelatedServices = relatedServices,
                Reviews = reviews,
                ShowOnSiteReviews = showOnSiteReviews,
            };

            this.ViewData["Title"] = $"Handyman & Plumbing in {area.Name} - HandyFix";
            this.ViewData["MetaDescription"] = $"Local handyman and plumbing services in {area.Name}. Transparent hourly pricing, no call-out fee, usually available within days. Book HandyFix online today.";
            this.ViewData["Canonical"] = this.Url.RouteUrl("AreaDetails", new { areaSlug = area.Slug }, this.Request.Scheme);
            this.ViewData["OgImage"] = $"{this.Request.Scheme}://{this.Request.Host}/images/areas/{area.Slug}-hero.webp";

            return this.View(model);
        }
    }
}
