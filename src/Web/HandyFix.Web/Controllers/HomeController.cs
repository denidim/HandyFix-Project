namespace HandyFix.Web.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Services.Data.Availability;
    using HandyFix.Services.Data.Inquiries;
    using HandyFix.Services.Data.Reviews;
    using HandyFix.Services.Data.Services;
    using HandyFix.Web.ViewModels;
    using HandyFix.Web.ViewModels.Home;
    using HandyFix.Web.ViewModels.Reviews;
    using HandyFix.Web.ViewModels.Services;

    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Mvc;

    public class HomeController : BaseController
    {
        private readonly IReviewsService reviewsService;
        private readonly IInquiriesService inquiriesService;
        private readonly IServicesService servicesService;
        private readonly IAvailabilityService availabilityService;
        private readonly IWebHostEnvironment environment;

        public HomeController(
            IReviewsService reviewsService,
            IInquiriesService inquiriesService,
            IServicesService servicesService,
            IAvailabilityService availabilityService,
            IWebHostEnvironment environment)
        {
            this.reviewsService = reviewsService;
            this.inquiriesService = inquiriesService;
            this.servicesService = servicesService;
            this.availabilityService = availabilityService;
            this.environment = environment;
        }

        public async Task<IActionResult> Index()
        {
            // For Hero widget
            var services = await this.servicesService.GetAllAsync<ServiceViewModel>();
            var dates = await this.availabilityService.GetAvailableDatesAsync();

            this.ViewData["Services"] = services;
            this.ViewData["AvailableDates"] = dates;

            // Approved Reviews for slider
            var sliderReviews = await this.reviewsService.GetLatestApprovedAsync<ReviewViewModel>(6);
            this.ViewData["SliderReviews"] = sliderReviews;

            // Popular Services
            var popularServices = services.Take(4).ToList();
            this.ViewData["PopularServices"] = popularServices;

            this.ViewData["Title"] = "Handy Fix - Plumbers & Handymen in South London";
            this.ViewData["MetaDescription"] = "Handy Fix provides reliable local plumbing and handyman services in Sutton, Croydon, Epsom, Bromley, Kingston, Kent, and South London. Book hourly slots online.";

            return this.View();
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

            var imageUrls = new List<string>();
            if (model.Images != null && model.Images.Count > 0)
            {
                var uploadsFolder = Path.Combine(this.environment.WebRootPath, "uploads", "inquiries");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                foreach (var image in model.Images)
                {
                    if (image.Length > 0)
                    {
                        var uniqueFileName = $"{Guid.NewGuid()}_{Path.GetFileName(image.FileName)}";
                        var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                        using (var fileStream = new FileStream(filePath, FileMode.Create))
                        {
                            await image.CopyToAsync(fileStream);
                        }

                        imageUrls.Add($"/uploads/inquiries/{uniqueFileName}");
                    }
                }
            }

            await this.inquiriesService.CreateInquiryAsync(model.Name, model.Email, model.PhoneNumber, model.Message, imageUrls);
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

        public IActionResult Privacy()
        {
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
