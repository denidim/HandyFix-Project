namespace HandyFix.Web.Areas.Administration.Controllers
{
    using System.Linq;

    using HandyFix.Data.Common.Repositories;
    using HandyFix.Data.Models;
    using HandyFix.Services.Data;
    using HandyFix.Web.ViewModels.Administration.Dashboard;

    using Microsoft.AspNetCore.Mvc;

    public class DashboardController : AdministrationController
    {
        private readonly ISettingsService settingsService;
        private readonly IDeletableEntityRepository<Booking> bookingRepository;
        private readonly IDeletableEntityRepository<Inquiry> inquiryRepository;
        private readonly IDeletableEntityRepository<Review> reviewRepository;
        private readonly IDeletableEntityRepository<Payment> paymentRepository;

        public DashboardController(
            ISettingsService settingsService,
            IDeletableEntityRepository<Booking> bookingRepository,
            IDeletableEntityRepository<Inquiry> inquiryRepository,
            IDeletableEntityRepository<Review> reviewRepository,
            IDeletableEntityRepository<Payment> paymentRepository)
        {
            this.settingsService = settingsService;
            this.bookingRepository = bookingRepository;
            this.inquiryRepository = inquiryRepository;
            this.reviewRepository = reviewRepository;
            this.paymentRepository = paymentRepository;
        }

        public IActionResult Index()
        {
            var viewModel = new IndexViewModel
            {
                SettingsCount = this.settingsService.GetCount(),
                TotalBookingsCount = this.bookingRepository.All().Count(),
                PendingBookingsCount = this.bookingRepository.All().Count(x => x.Status.Name == "Pending"),
                TotalEnquiriesCount = this.inquiryRepository.All().Count(),
                PendingReviewsCount = this.reviewRepository.AllWithDeleted().Count(x => !x.IsApproved),
                TotalRevenue = this.paymentRepository.All()
                    .Where(x => x.Status.Name == "DepositPaid" || x.Status.Name == "Completed")
                    .Sum(x => (decimal?)x.Amount) ?? 0.00m,
            };

            return this.View(viewModel);
        }
    }
}
