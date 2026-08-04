namespace HandyFix.Web.Areas.Administration.Controllers
{
    using System.Threading.Tasks;

    using HandyFix.Services.Data;
    using HandyFix.Services.Data.Bookings;
    using HandyFix.Services.Data.Inquiries;
    using HandyFix.Services.Data.Payments;
    using HandyFix.Services.Data.Reviews;
    using HandyFix.Web.ViewModels.Administration.Dashboard;

    using Microsoft.AspNetCore.Mvc;

    public class DashboardController : AdministrationController
    {
        private readonly ISettingsService settingsService;
        private readonly IBookingsService bookingsService;
        private readonly IInquiriesService inquiriesService;
        private readonly IReviewsService reviewsService;
        private readonly IPaymentsService paymentsService;

        public DashboardController(
            ISettingsService settingsService,
            IBookingsService bookingsService,
            IInquiriesService inquiriesService,
            IReviewsService reviewsService,
            IPaymentsService paymentsService)
        {
            this.settingsService = settingsService;
            this.bookingsService = bookingsService;
            this.inquiriesService = inquiriesService;
            this.reviewsService = reviewsService;
            this.paymentsService = paymentsService;
        }

        public async Task<IActionResult> Index()
        {
            var viewModel = new IndexViewModel
            {
                SettingsCount = this.settingsService.GetCount(),
                TotalBookingsCount = await this.bookingsService.GetTotalCountAsync(),
                PendingBookingsCount = await this.bookingsService.GetPendingCountAsync(),
                TotalEnquiriesCount = await this.inquiriesService.GetTotalCountAsync(),
                PendingReviewsCount = await this.reviewsService.GetPendingCountAsync(),
                TotalRevenue = await this.paymentsService.GetTotalRevenueAsync(),
            };

            return this.View(viewModel);
        }
    }
}
