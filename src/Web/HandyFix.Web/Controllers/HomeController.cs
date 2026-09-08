namespace HandyFix.Web.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
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

            this.ViewData["Title"] = "Handy Fix - Plumbers & Handymen in South London";
            this.ViewData["MetaDescription"] = "Handy Fix provides reliable local plumbing and handyman services in Sutton, Croydon, Epsom, Bromley, Kingston, Kent, and South London. Book hourly slots online.";

            return this.View(model);
        }

        [HttpGet]
        [Route("Contact")]
        public IActionResult Contact(string service = null)
        {
            this.ViewData["Title"] = "Contact Us - Emergency Plumbing & Handyman";
            this.ViewData["MetaDescription"] = "Get in touch with Handy Fix for a custom quote or emergency plumbing and handyman help across Sutton, Croydon, Epsom, and South London.";
            var model = new ContactInputModel();
            if (!string.IsNullOrWhiteSpace(service))
            {
                model.Message = $"Hi, I would like to request a quote / survey for: {service.Trim()}.\n\nProject details:\n";
            }

            return this.View(model);
        }

        [HttpPost]
        [Route("Contact")]
        public async Task<IActionResult> Contact(ContactInputModel model)
        {
            if (!this.ModelState.IsValid)
            {
                this.ViewData["Title"] = "Contact Us - Emergency Plumbing & Handyman";
                return this.View(model);
            }

            IReadOnlyList<string> imageUrls = await this.imageService.UploadImagesAsync(model.Images, "inquiries");
            await this.inquiriesService.CreateInquiryAsync(model, imageUrls);
            this.TempData["SuccessMessage"] = "Thank you! Your enquiry has been received. Our team will contact you shortly.";

            return this.RedirectToAction("Contact");
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

            this.ViewData["Title"] = "Customer Reviews - HandyFix London";
            this.ViewData["MetaDescription"] = "Read verified customer reviews for Handy Fix's plumbing and handyman services across South London, or leave your own.";
            return this.View(model);
        }

        [Route("About")]
        public IActionResult About()
        {
            this.ViewData["Title"] = "About Handy Fix - Professional Handyman Services";
            this.ViewData["MetaDescription"] = "Learn about Handy Fix, the fully insured plumbing and handyman team serving Sutton, Croydon, Epsom, and South London.";
            return this.View();
        }

        [Route("FAQ")]
        public IActionResult FAQ()
        {
            this.ViewData["Title"] = "Frequently Asked Questions - Handy Fix";
            this.ViewData["MetaDescription"] = "Answers to common questions about booking, pricing, and scheduling plumbing and handyman services with Handy Fix.";
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
            this.ViewData["Title"] = "Privacy Policy - Handy Fix";
            this.ViewData["MetaDescription"] = "How Handy Fix collects, uses, and protects your personal data.";
            return this.View();
        }

        [Route("TermsAndConditions")]
        public IActionResult Terms()
        {
            this.ViewData["Title"] = "Terms & Conditions - Handy Fix";
            this.ViewData["MetaDescription"] = "The terms and conditions governing bookings and service delivery with Handy Fix.";
            return this.View();
        }

        [Route("CookiePolicy")]
        public IActionResult CookiePolicy()
        {
            this.ViewData["Title"] = "Cookie Policy - Handy Fix";
            this.ViewData["MetaDescription"] = "How Handy Fix uses cookies on this website.";
            return this.View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return this.View(
                new ErrorViewModel { RequestId = Activity.Current?.Id ?? this.HttpContext.TraceIdentifier });
        }
    }
}
