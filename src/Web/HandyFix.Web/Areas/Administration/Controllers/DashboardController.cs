namespace HandyFix.Web.Areas.Administration.Controllers
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using HandyFix.Services.Data.Bookings;
    using HandyFix.Services.Data.Inquiries;
    using HandyFix.Services.Data.Payments;
    using HandyFix.Web.ViewModels.Administration.Dashboard;
    using HandyFix.Web.ViewModels.Booking;

    using Microsoft.AspNetCore.Mvc;

    public class DashboardController : AdministrationController
    {
        private readonly IBookingsService bookingsService;
        private readonly IInquiriesService inquiriesService;
        private readonly IPaymentsService paymentsService;

        public DashboardController(
            IBookingsService bookingsService,
            IInquiriesService inquiriesService,
            IPaymentsService paymentsService)
        {
            this.bookingsService = bookingsService;
            this.inquiriesService = inquiriesService;
            this.paymentsService = paymentsService;
        }

        public async Task<IActionResult> Index()
        {
            // Worked out the way the Jobs page works its own card out, from the same list, so the
            // two can never show different figures.
            IEnumerable<BookingDetailsViewModel> allBookings = await this.bookingsService.GetAllBookingsAsync<BookingDetailsViewModel>();
            BookingSummaryStats summary = this.bookingsService.GetSummaryStats(allBookings);

            var viewModel = new IndexViewModel
            {
                TotalBookingsCount = await this.bookingsService.GetTotalCountAsync(),
                PendingBookingsCount = await this.bookingsService.GetPendingCountAsync(),
                AwaitingTechnicianCount = summary.AwaitingTechnicianCount,
                TotalEnquiriesCount = await this.inquiriesService.GetTotalCountAsync(),
                TotalRevenue = await this.paymentsService.GetTotalRevenueAsync(),
            };

            return this.View(viewModel);
        }
    }
}
