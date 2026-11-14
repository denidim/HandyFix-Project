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
    using HandyFix.Web.ViewModels.Validation;

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
                "We work within 15 miles of Chessington, across Surrey and South London. Find your town to see the postcodes we cover there, then book a slot online.",
                this.Url.RouteUrl("Areas", null, this.Request.Scheme),
                PageTitle.BookOnline);

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
                TownDescription(area, model.PlumbingRate, model.HandymanRate),
                this.Url.RouteUrl("AreaDetails", new { areaSlug = area.Slug }, this.Request.Scheme),
                PageTitle.BookOnline);

            // An upload's address is already a full one; a picture that came with the site is
            // named on this site; an area with neither shares the site's own picture.
            if (picture != null)
            {
                this.ViewData["OgImage"] = picture.StartsWith('/') ? $"{this.Request.Scheme}://{this.Request.Host}{picture}" : picture;
            }

            return this.View(model);
        }

        // A town's description for search engines, made from its own record: its name, its
        // postcode districts and the two hourly rates the page quotes. No text is written per
        // town and the admin has no field for it, so a new town gets a true one by itself (the
        // SEO master plan's item 7). The one before promised "usually available within days",
        // which nobody had confirmed. A part the record lacks is left out.
        private static string TownDescription(ServiceAreaDetailsViewModel area, decimal? plumbingRate, decimal? handymanRate)
        {
            var districts = UkPostcode.ParseDistricts(area.PostcodeDistricts);
            var town = districts.Count == 0 ? area.Name : $"{area.Name} ({string.Join(", ", districts)})";
            var rates = plumbingRate.HasValue && handymanRate.HasValue
                ? $"Plumbing £{plumbingRate:F0} an hour, handyman £{handymanRate:F0}, no call-out fee. "
                : string.Empty;

            return $"Plumber and handyman in {town}. {rates}Pick a time that suits you and book online.";
        }
    }
}
