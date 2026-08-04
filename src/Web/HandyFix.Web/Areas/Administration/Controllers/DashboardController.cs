namespace HandyFix.Web.Areas.Administration.Controllers
{
    using System.Threading.Tasks;

    using HandyFix.Services.Data.Bookings;
    using HandyFix.Services.Data.Inquiries;
    using HandyFix.Services.Data.Payments;
    using HandyFix.Services.Data.Reviews;
    using HandyFix.Web.ViewModels.Administration.Dashboard;

    using Microsoft.AspNetCore.Mvc;

    public class DashboardController : AdministrationController
    {
        private readonly IBookingsService bookingsService;
        private readonly IInquiriesService inquiriesService;
        private readonly IReviewsService reviewsService;
        private readonly IPaymentsService paymentsService;

        public DashboardController(
            IBookingsService bookingsService,
            IInquiriesService inquiriesService,
            IReviewsService reviewsService,
            IPaymentsService paymentsService)
        {
            this.bookingsService = bookingsService;
            this.inquiriesService = inquiriesService;
            this.reviewsService = reviewsService;
            this.paymentsService = paymentsService;
        }

        public async Task<IActionResult> Index()
        {
            var viewModel = new IndexViewModel
            {
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
