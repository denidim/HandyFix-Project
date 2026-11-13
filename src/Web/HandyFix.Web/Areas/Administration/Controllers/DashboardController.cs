namespace HandyFix.Web.Areas.Administration.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using HandyFix.Services.Data.Bookings;
    using HandyFix.Services.Data.EmailsNotSent;
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
        private readonly IEmailsNotSentService emailsNotSentService;

        public DashboardController(
            IBookingsService bookingsService,
            IInquiriesService inquiriesService,
            IPaymentsService paymentsService,
            IEmailsNotSentService emailsNotSentService)
        {
            this.bookingsService = bookingsService;
            this.inquiriesService = inquiriesService;
            this.paymentsService = paymentsService;
            this.emailsNotSentService = emailsNotSentService;
        }

        // heardAbout picks the period of the "How customers heard about us" table: "all" for all
        // time, anything else (or nothing) for the last 30 days.
        public async Task<IActionResult> Index(string heardAbout = null)
        {
            bool allTime = heardAbout == IndexViewModel.HeardAboutAllTime;
            DateTime? heardAboutSince = allTime ? null : DateTime.UtcNow.AddDays(-IndexViewModel.HeardAboutDays);

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
                EmailsNotSent = await this.emailsNotSentService.GetSinceAsync<EmailNotSentViewModel>(
                    DateTime.UtcNow.AddDays(-IndexViewModel.EmailsNotSentDays)),
                HeardAbout = HeardAboutCountViewModel.Table(
                    await this.inquiriesService.GetHeardAboutCountsAsync(heardAboutSince),
                    await this.bookingsService.GetHeardAboutCountsAsync(heardAboutSince)),
                HeardAboutIsAllTime = allTime,
            };

            return this.View(viewModel);
        }
    }
}
