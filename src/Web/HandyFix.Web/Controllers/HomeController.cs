namespace HandyFix.Web.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Services;
    using HandyFix.Services.Data.Categories;
    using HandyFix.Services.Data.Inquiries;
    using HandyFix.Services.Data.Reviews;
    using HandyFix.Services.Data.Services;
    using HandyFix.Web.ViewModels;
    using HandyFix.Web.ViewModels.Home;
    using HandyFix.Web.ViewModels.Reviews;
    using HandyFix.Web.ViewModels.Services;

    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Configuration;

    [AllowAnonymous]
    public class HomeController : BaseController
    {
        private readonly IReviewsService reviewsService;
        private readonly IInquiriesService inquiriesService;
        private readonly IServicesService servicesService;
        private readonly ICategoriesService categoriesService;
        private readonly IImageService imageService;
        private readonly IConfiguration configuration;

        public HomeController(
            IReviewsService reviewsService,
            IInquiriesService inquiriesService,
            IServicesService servicesService,
            ICategoriesService categoriesService,
            IImageService imageService,
            IConfiguration configuration)
        {
            this.reviewsService = reviewsService;
            this.inquiriesService = inquiriesService;
            this.servicesService = servicesService;
            this.categoriesService = categoriesService;
            this.imageService = imageService;
            this.configuration = configuration;
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
                PopularServices = services.Take(4).ToList(),
            };

            this.ViewData["Title"] = "Plumbers & Handymen in Surrey & South London";
            this.ViewData["MetaDescription"] = "Plumbing Handyman Surrey provides reliable local plumbing and handyman services across Surrey and South London, including Chessington, Cobham, Esher, Guildford, Epsom, and Kingston. Book hourly slots online.";

            return this.View(model);
        }

        [HttpGet]
        [Route("Contact")]
        public async Task<IActionResult> Contact(string service = null, string date = null)
        {
            this.ViewData["Title"] = "Contact Us - Emergency Plumbing & Handyman";
            this.ViewData["MetaDescription"] = "Get in touch with Plumbing Handyman Surrey for a custom quote or emergency plumbing and handyman help across Surrey and South London, including Chessington, Cobham, and Epsom.";
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

            await this.SetContactCategoriesAsync();
            return this.View(model);
        }

        [HttpPost]
        [Route("Contact")]
        public async Task<IActionResult> Contact(ContactInputModel model)
        {
            if (!this.ModelState.IsValid)
            {
                this.ViewData["Title"] = "Contact Us - Emergency Plumbing & Handyman";
                await this.SetContactCategoriesAsync();
                return this.View(model);
            }

            IReadOnlyList<string> imageUrls = await this.imageService.UploadImagesAsync(model.Images, "inquiries");
            await this.inquiriesService.CreateInquiryAsync(model, imageUrls);
            this.TempData["SuccessMessage"] = "Thank you! Your enquiry has been received. Our team will contact you shortly.";

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
        public async Task<IActionResult> JoinTeam(JoinTeamInputModel model)
        {
            if (!this.ModelState.IsValid)
            {
                this.SetJoinTeamMetadata();
                return this.View(model);
            }

            // Saved as an enquiry with no photos, so applications reach the existing admin
            // Enquiries list without a table of their own - see JoinTeamInputModel.
            await this.inquiriesService.CreateInquiryAsync(model.ToContactInputModel(), Array.Empty<string>());
            this.TempData["SuccessMessage"] = "Thanks for applying! We've received your details and will be in touch soon.";

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
                ShowOnSiteReviews = showOnSiteReviews,
            };

            this.ViewData["Title"] = "Customer Reviews";
            this.ViewData["MetaDescription"] = "Read verified customer reviews of our plumbing and handyman services across Surrey and South London, or leave your own.";
            return this.View(model);
        }

        [Route("About")]
        public IActionResult About()
        {
            this.ViewData["Title"] = "About Us";
            this.ViewData["MetaDescription"] = "Learn about Plumbing Handyman Surrey, the dedicated plumbing, handyman and refurbishment specialists serving Surrey and South London from Chessington.";
            return this.View();
        }

        [Route("FAQ")]
        public IActionResult FAQ()
        {
            this.ViewData["Title"] = "Frequently Asked Questions";
            this.ViewData["MetaDescription"] = "Answers to common questions about booking, pricing, and scheduling plumbing and handyman services with Plumbing Handyman Surrey.";
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
            this.ViewData["Title"] = "Privacy Policy";
            this.ViewData["MetaDescription"] = "How Plumbing Handyman Surrey collects, uses, and protects your personal data.";
            return this.View();
        }

        [Route("TermsAndConditions")]
        public IActionResult Terms()
        {
            this.ViewData["Title"] = "Terms & Conditions";
            this.ViewData["MetaDescription"] = "The terms and conditions governing bookings and service delivery with Plumbing Handyman Surrey.";
            return this.View();
        }

        [Route("CookiePolicy")]
        public IActionResult CookiePolicy()
        {
            this.ViewData["Title"] = "Cookie Policy";
            this.ViewData["MetaDescription"] = "How Plumbing Handyman Surrey uses cookies on this website.";
            return this.View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return this.View(
                new ErrorViewModel { RequestId = Activity.Current?.Id ?? this.HttpContext.TraceIdentifier });
        }

        private void SetJoinTeamMetadata()
        {
            this.ViewData["Title"] = "Join Our Team - Careers";
            this.ViewData["MetaDescription"] = "Skilled plumber, handyman or building tradesperson in Surrey or South London? Apply to join our Chessington-based team.";
        }

        // The Contact form's category options come from the real service categories, so a category
        // added through the admin panel appears there without a code change.
        private async Task SetContactCategoriesAsync()
        {
            IEnumerable<CategoryViewModel> categories = await this.categoriesService.GetAllAsync<CategoryViewModel>();
            this.ViewData["ContactCategories"] = categories.Select(c => c.Name).ToList();
        }
    }
}
