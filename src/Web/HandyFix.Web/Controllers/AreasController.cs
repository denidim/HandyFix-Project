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

    [AllowAnonymous]
    public class AreasController : BaseController
    {
        private readonly IServiceAreasService areasService;
        private readonly IServicesService servicesService;
        private readonly IReviewsService reviewsService;
        private readonly IImageStorageService imageStorage;
        private readonly IWebHostEnvironment environment;

        public AreasController(
            IServiceAreasService areasService,
            IServicesService servicesService,
            IReviewsService reviewsService,
            IImageStorageService imageStorage,
            IWebHostEnvironment environment)
        {
            this.areasService = areasService;
            this.servicesService = servicesService;
            this.reviewsService = reviewsService;
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

            List<ServiceViewModel> plumbingServices = (await this.servicesService.GetByCategoryAsync<ServiceViewModel>("Plumbing")).ToList();
            List<ServiceViewModel> handymanServices = (await this.servicesService.GetByCategoryAsync<ServiceViewModel>("Handyman")).ToList();

            // The town's own two plumbing and two handyman services, picked from its slug as the
            // database holds it, not as the address was typed (Section 3el). Every town showed
            // the first two of each until then, so the area pages carried the same four cards.
            var relatedServices = AreaServices.For(area.Slug, plumbingServices, handymanServices);

            // Only the reviews from this town (Section 3eh): the same three on every area page
            // would be the same text on 25 pages, and a review from the town is the proof its
            // page needs. With none the page leaves the section out.
            IEnumerable<ReviewViewModel> reviews = await this.reviewsService.GetLatestApprovedInTownAsync<ReviewViewModel>(area.Name, 3);

            var picture = PictureLookups.AreaPictureOf(this.environment, area.Slug, area.HeroImageUrl);
            var model = new ServiceAreaDetailsPageViewModel
            {
                Area = area,
                RelatedServices = relatedServices.WithMediumCopies(this.imageStorage),
                Reviews = reviews,

                // The two hourly rates the line under the town's text quotes, worked out as the
                // Pricing page does: the lowest price among the category's active services.
                PlumbingRate = plumbingServices.Count == 0 ? null : plumbingServices.Min(s => s.BasePrice),
                HandymanRate = handymanServices.Count == 0 ? null : handymanServices.Min(s => s.BasePrice),
                Picture = picture,
                PhonePicture = PictureLookups.AreaPhonePictureOf(this.environment, this.imageStorage, area.Slug, area.HeroImageUrl),
            };

            this.SetPageMeta(
                $"Plumber & Handyman in {area.Name}",
                $"Local handyman and plumbing services in {area.Name}. Transparent hourly pricing, no call-out fee, usually available within days. Book online today.",
                this.Url.RouteUrl("AreaDetails", new { areaSlug = area.Slug }, this.Request.Scheme));

            // An upload's address is already a full one; a picture that came with the site is
            // named on this site; an area with neither shares the site's own picture.
            if (picture != null)
            {
                this.ViewData["OgImage"] = picture.StartsWith('/') ? $"{this.Request.Scheme}://{this.Request.Host}{picture}" : picture;
            }

            return this.View(model);
        }
    }
}
