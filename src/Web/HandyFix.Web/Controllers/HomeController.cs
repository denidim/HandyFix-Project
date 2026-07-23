namespace HandyFix.Web.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Services;
    using HandyFix.Services.Data.Availability;
    using HandyFix.Services.Data.Inquiries;
    using HandyFix.Services.Data.Reviews;
    using HandyFix.Services.Data.Services;
    using HandyFix.Web.ViewModels;
    using HandyFix.Web.ViewModels.Home;
    using HandyFix.Web.ViewModels.Reviews;
    using HandyFix.Web.ViewModels.Services;

    using Microsoft.AspNetCore.Mvc;

    public class HomeController : BaseController
    {
        private readonly IReviewsService reviewsService;
        private readonly IInquiriesService inquiriesService;
        private readonly IServicesService servicesService;
        private readonly IAvailabilityService availabilityService;
        private readonly IImageService imageService;

        public HomeController(
            IReviewsService reviewsService,
            IInquiriesService inquiriesService,
            IServicesService servicesService,
            IAvailabilityService availabilityService,
            IImageService imageService)
        {
            this.reviewsService = reviewsService;
            this.inquiriesService = inquiriesService;
            this.servicesService = servicesService;
            this.availabilityService = availabilityService;
            this.imageService = imageService;
        }

        public async Task<IActionResult> Index()
        {
            // For Hero widget
            var services = await this.servicesService.GetAllAsync<ServiceViewModel>();
            var dates = await this.availabilityService.GetAvailableDatesAsync();

            // Approved Reviews for slider
            var sliderReviews = await this.reviewsService.GetLatestApprovedAsync<ReviewViewModel>(6);

            var model = new HomeIndexViewModel
            {
                Services = services,
                AvailableDates = dates,
                SliderReviews = sliderReviews,
                PopularServices = services.Take(4).ToList(),
            };

            this.ViewData["Title"] = "Handy Fix - Plumbers & Handymen in South London";
            this.ViewData["MetaDescription"] = "Handy Fix provides reliable local plumbing and handyman services in Sutton, Croydon, Epsom, Bromley, Kingston, Kent, and South London. Book hourly slots online.";

            return this.View(model);
        }

        [HttpGet]
        [Route("Contact")]
        public IActionResult Contact()
        {
            this.ViewData["Title"] = "Contact Us - Emergency Plumbing & Handyman";
            return this.View(new ContactInputModel());
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

            var imageUrls = await this.imageService.UploadImagesAsync(model.Images, "inquiries");
            await this.inquiriesService.CreateInquiryAsync(model, imageUrls);
            this.TempData["SuccessMessage"] = "Thank you! Your enquiry has been received. Our team will contact you shortly.";

            return this.RedirectToAction("Contact");
        }

        [Route("Reviews")]
        public async Task<IActionResult> Reviews()
        {
            var approvedReviews = await this.reviewsService.GetLatestApprovedAsync<ReviewViewModel>(50);
            var model = new ReviewsListViewModel
            {
                Reviews = approvedReviews,
                NewReview = new ReviewInputModel(),
            };

            this.ViewData["Title"] = "Customer Reviews - HandyFix London";
            return this.View(model);
        }

        [Route("About")]
        public IActionResult About()
        {
            this.ViewData["Title"] = "About Handy Fix - Professional Handyman Services";
            return this.View();
        }

        [Route("FAQ")]
        public IActionResult FAQ()
        {
            this.ViewData["Title"] = "Frequently Asked Questions - Handy Fix";
            return this.View();
        }

        [Route("ServiceAreas")]
        public IActionResult ServiceAreas()
        {
            this.ViewData["Title"] = "Service Coverage Areas - South London, Surrey & Kent";
            return this.View();
        }

        [Route("PrivacyPolicy")]
        public IActionResult Privacy()
        {
            this.ViewData["Title"] = "Privacy Policy - Handy Fix";
            return this.View();
        }

        [Route("TermsAndConditions")]
        public IActionResult Terms()
        {
            this.ViewData["Title"] = "Terms & Conditions - Handy Fix";
            return this.View();
        }

        [Route("CookiePolicy")]
        public IActionResult CookiePolicy()
        {
            this.ViewData["Title"] = "Cookie Policy - Handy Fix";
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
