namespace HandyFix.Web.Controllers
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Services;
    using HandyFix.Services.Data.Reviews;
    using HandyFix.Services.Data.ServiceAreas;
    using HandyFix.Services.Data.Services;
    using HandyFix.Web.Presentation;
    using HandyFix.Web.ViewModels.Reviews;
    using HandyFix.Web.ViewModels.ServiceAreas;
    using HandyFix.Web.ViewModels.Services;

    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Configuration;

    [AllowAnonymous]
    public class AreasController : BaseController
    {
        private readonly IServiceAreasService areasService;
        private readonly IServicesService servicesService;
        private readonly IReviewsService reviewsService;
        private readonly IConfiguration configuration;
        private readonly IImageStorageService imageStorage;
        private readonly IWebHostEnvironment environment;

        public AreasController(
            IServiceAreasService areasService,
            IServicesService servicesService,
            IReviewsService reviewsService,
            IConfiguration configuration,
            IImageStorageService imageStorage,
            IWebHostEnvironment environment)
        {
            this.areasService = areasService;
            this.servicesService = servicesService;
            this.reviewsService = reviewsService;
            this.configuration = configuration;
            this.imageStorage = imageStorage;
            this.environment = environment;
        }

        [Route("Areas", Name = "Areas")]
        public async Task<IActionResult> Index()
        {
            IEnumerable<ServiceAreaViewModel> areas = await this.areasService.GetAllAsync<ServiceAreaViewModel>();

            this.SetPageMeta(
                "Areas We Cover in Surrey & South London",
                "See every town and village Plumbing Handyman Surrey covers across South London and Surrey, from Chessington and Kingston out to Guildford and Cobham. Find your area and book online.",
                this.Url.RouteUrl("Areas", null, this.Request.Scheme));

            return this.View(areas);
        }

        [Route("Areas/{areaSlug}", Name = "AreaDetails")]
        public async Task<IActionResult> Details(string areaSlug)
        {
            ServiceAreaDetailsViewModel area = await this.areasService.GetBySlugAsync<ServiceAreaDetailsViewModel>(areaSlug);
            if (area == null)
            {
                return this.NotFound();
            }

            // Guaranteed FAQ ordering - see ServiceAreaDetailsViewModel for why this
            // isn't done via a custom Mapster collection mapping.
            area.Faqs = area.Faqs.OrderBy(x => x.DisplayOrder).ToList();

            IEnumerable<ServiceViewModel> plumbingServices = await this.servicesService.GetByCategoryAsync<ServiceViewModel>("Plumbing");
            IEnumerable<ServiceViewModel> handymanServices = await this.servicesService.GetByCategoryAsync<ServiceViewModel>("Handyman");
            var relatedServices = plumbingServices.Take(2).Concat(handymanServices.Take(2)).ToList();

            // Hidden on-site until the Google Business Profile import ships, see
            // Business:ShowOnSiteReviews.
            var showOnSiteReviews = this.configuration.GetValue<bool>("Business:ShowOnSiteReviews");
            IEnumerable<ReviewViewModel> reviews = showOnSiteReviews
                ? await this.reviewsService.GetLatestApprovedAsync<ReviewViewModel>(3)
                : Enumerable.Empty<ReviewViewModel>();

            var model = new ServiceAreaDetailsPageViewModel
            {
                Area = area,
                RelatedServices = relatedServices.WithMediumCopies(this.imageStorage),
                Reviews = reviews,
                ShowOnSiteReviews = showOnSiteReviews,
                PhonePicture = PictureLookups.PhonePictureOf(this.environment, area.Slug),
            };

            this.SetPageMeta(
                $"Handyman & Plumbing in {area.Name}",
                $"Local handyman and plumbing services in {area.Name}. Transparent hourly pricing, no call-out fee, usually available within days. Book online today.",
                this.Url.RouteUrl("AreaDetails", new { areaSlug = area.Slug }, this.Request.Scheme));
            this.ViewData["OgImage"] = $"{this.Request.Scheme}://{this.Request.Host}/images/areas/{area.Slug}-hero.webp";

            return this.View(model);
        }
    }
}
