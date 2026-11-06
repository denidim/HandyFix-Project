namespace HandyFix.Web.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Services;
    using HandyFix.Services.Data.Categories;
    using HandyFix.Services.Data.Inquiries;
    using HandyFix.Services.Data.Reviews;
    using HandyFix.Services.Data.Services;
    using HandyFix.Web.Services.Forms;
    using HandyFix.Web.ViewModels;
    using HandyFix.Web.ViewModels.Home;
    using HandyFix.Web.ViewModels.Reviews;
    using HandyFix.Web.ViewModels.Services;

    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.RateLimiting;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;

    [AllowAnonymous]
    public class HomeController : BaseController
    {
        private const int PopularServicesCount = 4;

        // One wording each, whatever happened behind it: saved, a repeat of one just saved, or
        // dropped as a program's. A reply that differed would say which.
        private const string EnquiryReceivedMessage = "Thank you! Your enquiry has been received. Our team will contact you shortly.";
        private const string ApplicationReceivedMessage = "Thanks for applying! We've received your details and will be in touch soon.";

        // The pictures of the home page's three division cards.
        private static readonly string[] DivisionPictures =
        {
            "/images/services/plumbing-category-hero.webp",
            "/images/services/handyman-category-hero.webp",
            "/images/services/small-building-category-hero.webp",
        };

        private readonly IReviewsService reviewsService;
        private readonly IInquiriesService inquiriesService;
        private readonly IServicesService servicesService;
        private readonly ICategoriesService categoriesService;
        private readonly IImageService imageService;
        private readonly IImageStorageService imageStorage;
        private readonly IFormGuard formGuard;
        private readonly IConfiguration configuration;
        private readonly ILogger<HomeController> logger;

        public HomeController(
            IReviewsService reviewsService,
            IInquiriesService inquiriesService,
            IServicesService servicesService,
            ICategoriesService categoriesService,
            IImageService imageService,
            IImageStorageService imageStorage,
            IFormGuard formGuard,
            IConfiguration configuration,
            ILogger<HomeController> logger)
        {
            this.reviewsService = reviewsService;
            this.inquiriesService = inquiriesService;
            this.servicesService = servicesService;
            this.categoriesService = categoriesService;
            this.imageService = imageService;
            this.imageStorage = imageStorage;
            this.formGuard = formGuard;
            this.configuration = configuration;
            this.logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            // For Hero widget
            IEnumerable<ServiceViewModel> services = await this.servicesService.GetAllAsync<ServiceViewModel>();
            IEnumerable<CategoryViewModel> categories = await this.categoriesService.GetAllAsync<CategoryViewModel>();

            // Approved Reviews for slider — hidden on-site until the Google Business Profile
            // import ships, see Business:ShowOnSiteReviews.
            var showOnSiteReviews = this.configuration.GetValue<bool>("Business:ShowOnSiteReviews");
            IEnumerable<ReviewViewModel> sliderReviews = showOnSiteReviews
                ? await this.reviewsService.GetLatestApprovedAsync<ReviewViewModel>(6)
                : Enumerable.Empty<ReviewViewModel>();

            var model = new HomeIndexViewModel
            {
                Services = services,
                Categories = categories,
                SliderReviews = sliderReviews,
                ShowOnSiteReviews = showOnSiteReviews,
                PopularServices = PickPopularServices(services),
            };

            // Each card picture's middle copy, looked up once here; the page used to look each one
            // up twice while it was drawn (PROJECT_STATE.md Section 3dj).
            model.MediumCopies = DivisionPictures
                .Concat(model.PopularServices.Select(s => s.ImageUrl))
                .Where(picture => picture != null)
                .Distinct()
                .ToDictionary(picture => picture, picture => this.imageStorage.GetMediumCopyPublicUrl(picture));

            this.SetPageMeta(
                "Plumbers & Handymen in Surrey & South London",
                "Plumbing Handyman Surrey provides reliable local plumbing and handyman services across Surrey and South London, including Chessington, Cobham, Esher, Guildford, Epsom, and Kingston. Book hourly slots online.",
                this.Url.Action("Index", "Home", null, this.Request.Scheme));

            return this.View(model);
        }

        [HttpGet]
        [Route("Contact")]
        public async Task<IActionResult> Contact(string service = null, string date = null, string categorySlug = null)
        {
            this.SetContactPageMeta();
            var model = new ContactInputModel();
            if (!string.IsNullOrWhiteSpace(service))
            {
                // A date comes from the booking page's "Send an Enquiry" (PROJECT_STATE Section 3bp):
                // the visitor wanted that day and found no time that suits, so the message asks about
                // the day rather than requesting a quote. A date that doesn't parse is ignored.
                if (DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime preferredDate))
                {
                    model.Message = $"Hi, I'd like to book {service.Trim()} on {preferredDate.ToString("ddd d MMM", CultureInfo.InvariantCulture)}, but I couldn't find a time that works in the booking calendar. Could you fit me in?\n\n";
                }
                else
                {
                    model.Message = $"Hi, I would like to request a quote / survey for: {service.Trim()}.\n\nProject details:\n";
                }

                // Pre-select the category of the service the visitor came from, so a building quote
                // request isn't filed under whichever category happens to be listed first.
                IEnumerable<ServiceViewModel> services = await this.servicesService.GetAllAsync<ServiceViewModel>();
                model.Category = services
                    .FirstOrDefault(s => string.Equals(s.Name, service.Trim(), StringComparison.OrdinalIgnoreCase))
                    ?.CategoryName;
            }

            IEnumerable<CategoryViewModel> categories = await this.SetContactCategoriesAsync(model);

            // The home booking widget sends a category's slug, with no service, when a quote-only
            // category is chosen there (PROJECT_STATE Section 3bz). A slug that matches nothing
            // leaves the list unselected. The parameter is not called "category": the form's
            // list reads a posted or query value of that name before it reads the model, and
            // would look for an option whose value is the slug.
            if (model.Category == null && !string.IsNullOrWhiteSpace(categorySlug))
            {
                model.Category = categories
                    .FirstOrDefault(c => string.Equals(c.Slug, categorySlug.Trim(), StringComparison.OrdinalIgnoreCase))
                    ?.Name;
            }

            return this.View(model);
        }

        [HttpPost]
        [Route("Contact")]
        [EnableRateLimiting(RateLimits.FormsPolicy)]
        public async Task<IActionResult> Contact(ContactInputModel model)
        {
            if (!this.ModelState.IsValid)
            {
                return await this.RedisplayContactFormAsync(model);
            }

            // A submission that carries a program's mark gets the same thank-you as a real one and
            // is not saved, uploaded or emailed about: the program learns nothing from the reply.
            FormGuardResult guard = await this.formGuard.CheckAsync(this.HttpContext, FormNames.Contact);
            if (guard == FormGuardResult.Automated)
            {
                this.TempData["SuccessMessage"] = EnquiryReceivedMessage;
                return this.RedirectToAction("Contact");
            }

            // The "are you a person" check did not pass. A person can land here, so it is said
            // out loud: the form comes back as typed, with a message and a fresh check.
            if (guard == FormGuardResult.ChallengeFailed)
            {
                this.ModelState.AddModelError(string.Empty, FormNames.ChallengeFailedMessage);
                return await this.RedisplayContactFormAsync(model);
            }

            // The same enquiry sent a second time (a double click, a resend after Back) is thanked
            // like the first and not saved again. Asked before the photos go to storage, so a
            // repeat does not upload them twice either.
            if (!await this.inquiriesService.IsRecentDuplicateAsync(model))
            {
                IReadOnlyList<string> imageUrls;
                try
                {
                    imageUrls = await this.imageService.UploadImagesAsync(model.Images, "inquiries");
                }
                catch (ImageUploadValidationException ex)
                {
                    // The visitor's to put right: too many photos, one too large, a file that is
                    // not a picture. The form comes back with what they typed and the reason.
                    this.ModelState.AddModelError(nameof(model.Images), ex.Message);
                    return await this.RedisplayContactFormAsync(model);
                }
                catch (Exception ex)
                {
                    // Storage is out of reach, which is not the visitor's to fix. The enquiry is
                    // worth more than its photos: it is saved without them, and the notice to the
                    // company says photos were lost. This used to end in an error page with
                    // nothing saved (PROJECT_STATE Section 3cb).
                    this.logger.LogError(ex, "Enquiry photos could not be stored; saving the enquiry without them");
                    imageUrls = Array.Empty<string>();
                    model.PhotosNotSaved = model.Images?.Count(f => f.Length > 0) ?? 0;
                }

                await this.inquiriesService.CreateInquiryAsync(model, imageUrls);
            }

            this.TempData["SuccessMessage"] = model.PhotosNotSaved > 0
                ? "Thank you! Your enquiry has been received, but your photos could not be uploaded this time. Our team will contact you shortly and will ask for them if they are needed."
                : EnquiryReceivedMessage;

            return this.RedirectToAction("Contact");
        }

        [HttpGet]
        [Route("JoinOurTeam")]
        public IActionResult JoinTeam()
        {
            this.SetJoinTeamMetadata();
            return this.View(new JoinTeamInputModel());
        }

        [HttpPost]
        [Route("JoinOurTeam")]
        [EnableRateLimiting(RateLimits.FormsPolicy)]
        public async Task<IActionResult> JoinTeam(JoinTeamInputModel model)
        {
            if (!this.ModelState.IsValid)
            {
                this.SetJoinTeamMetadata();
                return this.View(model);
            }

            // As on the Contact form: a program's submission is thanked and dropped.
            FormGuardResult guard = await this.formGuard.CheckAsync(this.HttpContext, FormNames.JoinTeam);
            if (guard == FormGuardResult.Automated)
            {
                this.TempData["SuccessMessage"] = ApplicationReceivedMessage;
                return this.RedirectToAction("JoinTeam");
            }

            if (guard == FormGuardResult.ChallengeFailed)
            {
                this.ModelState.AddModelError(string.Empty, FormNames.ChallengeFailedMessage);
                this.SetJoinTeamMetadata();
                return this.View(model);
            }

            // Saved as an enquiry with no photos, so applications reach the existing admin
            // Enquiries list without a table of their own - see JoinTeamInputModel. The same
            // application sent twice is saved once, as on the Contact form.
            ContactInputModel application = model.ToContactInputModel();
            if (!await this.inquiriesService.IsRecentDuplicateAsync(application))
            {
                await this.inquiriesService.CreateInquiryAsync(application, Array.Empty<string>());
            }

            this.TempData["SuccessMessage"] = ApplicationReceivedMessage;

            return this.RedirectToAction("JoinTeam");
        }

        [Route("Reviews")]
        public async Task<IActionResult> Reviews()
        {
            var showOnSiteReviews = this.configuration.GetValue<bool>("Business:ShowOnSiteReviews");
            IEnumerable<ReviewViewModel> approvedReviews = showOnSiteReviews
                ? await this.reviewsService.GetLatestApprovedAsync<ReviewViewModel>(50)
                : Enumerable.Empty<ReviewViewModel>();

            var model = new ReviewsListViewModel
            {
                Reviews = approvedReviews,
                GoogleReviewsUrl = this.configuration["Business:GoogleReviewsUrl"],
                GoogleLeaveReviewUrl = this.configuration["Business:GoogleLeaveReviewUrl"],
                ShowOnSiteReviews = showOnSiteReviews,
            };

            this.SetPageMeta(
                "Customer Reviews",
                "Read verified customer reviews of our plumbing and handyman services across Surrey and South London, or leave your own.",
                this.Url.Action("Reviews", "Home", null, this.Request.Scheme));
            return this.View(model);
        }

        [Route("About")]
        public IActionResult About()
        {
            this.SetPageMeta(
                "About Us",
                "Learn about Plumbing Handyman Surrey, the dedicated plumbing, handyman and refurbishment specialists serving Surrey and South London from Chessington.",
                this.Url.Action("About", "Home", null, this.Request.Scheme));
            return this.View();
        }

        [Route("FAQ")]
        public IActionResult FAQ()
        {
            this.SetPageMeta(
                "Frequently Asked Questions",
                "Answers to common questions about booking, pricing, and scheduling plumbing and handyman services with Plumbing Handyman Surrey.",
                this.Url.Action("FAQ", "Home", null, this.Request.Scheme));
            return this.View();
        }

        // Superseded by the dynamic Our Areas feature (AreasController); redirected
        // (not removed) so any existing links/bookmarks to the old static page keep
        // their SEO equity instead of hitting a dead end.
        [Route("ServiceAreas")]
        public IActionResult ServiceAreas()
        {
            return this.RedirectToRoutePermanent("Areas");
        }

        [Route("PrivacyPolicy")]
        public IActionResult Privacy()
        {
            this.SetPageMeta(
                "Privacy Policy",
                "How Plumbing Handyman Surrey collects, uses, and protects your personal data.",
                this.Url.Action("Privacy", "Home", null, this.Request.Scheme));
            return this.View();
        }

        [Route("TermsAndConditions")]
        public IActionResult Terms()
        {
            this.SetPageMeta(
                "Terms & Conditions",
                "The terms and conditions governing bookings and service delivery with Plumbing Handyman Surrey.",
                this.Url.Action("Terms", "Home", null, this.Request.Scheme));
            return this.View();
        }

        [Route("CookiePolicy")]
        public IActionResult CookiePolicy()
        {
            this.SetPageMeta(
                "Cookie Policy",
                "How Plumbing Handyman Surrey uses cookies on this website.",
                this.Url.Action("CookiePolicy", "Home", null, this.Request.Scheme));
            return this.View();
        }

        // The home page's grid holds four. The services an admin marked popular come first, in
        // list order; with fewer than four marked, the next services in the list fill the rest,
        // so the grid is never part-empty (PROJECT_STATE Section 3ca).
        private static List<ServiceViewModel> PickPopularServices(IEnumerable<ServiceViewModel> services)
        {
            return services
                .Where(s => s.IsPopular)
                .Concat(services.Where(s => !s.IsPopular))
                .Take(PopularServicesCount)
                .ToList();
        }

        // The form shown again keeps the page's description and address. It used to lose both, and
        // set a title the view replaces with its own "Get a Custom Quote" (PROJECT_STATE.md
        // Section 3dj).
        private async Task<IActionResult> RedisplayContactFormAsync(ContactInputModel model)
        {
            this.SetContactPageMeta();
            await this.SetContactCategoriesAsync(model);
            return this.View(model);
        }

        // The title is the view's own.
        private void SetContactPageMeta()
        {
            this.SetPageMeta(
                null,
                "Get in touch with Plumbing Handyman Surrey for a custom quote or emergency plumbing and handyman help across Surrey and South London, including Chessington, Cobham, and Epsom.",
                this.Url.Action("Contact", "Home", null, this.Request.Scheme));
        }

        private void SetJoinTeamMetadata()
        {
            this.SetPageMeta(
                "Join Our Team - Careers",
                "Skilled plumber, handyman or building tradesperson in Surrey or South London? Apply to join our Chessington-based team.",
                this.Url.Action("JoinTeam", "Home", null, this.Request.Scheme));
        }

        // The Contact form's category options come from the real service categories, so a category
        // added through the admin panel appears there without a code change.
        private async Task<IEnumerable<CategoryViewModel>> SetContactCategoriesAsync(ContactInputModel model)
        {
            IEnumerable<CategoryViewModel> categories = await this.categoriesService.GetAllAsync<CategoryViewModel>();
            model.Categories = categories.Select(c => c.Name).ToList();
            return categories;
        }
    }
}
