namespace HandyFix.Web.Areas.Administration.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using HandyFix.Common;
    using HandyFix.Services.Data.Bookings;
    using HandyFix.Services.Data.EmailsNotSent;
    using HandyFix.Services.Data.Inquiries;
    using HandyFix.Services.Data.Payments;
    using HandyFix.Services.Data.SiteCounts;
    using HandyFix.Web.Services.Counts;
    using HandyFix.Web.ViewModels.Administration.Dashboard;
    using HandyFix.Web.ViewModels.Booking;

    using Microsoft.AspNetCore.Mvc;

    public class DashboardController : AdministrationController
    {
        private readonly IBookingsService bookingsService;
        private readonly IInquiriesService inquiriesService;
        private readonly IPaymentsService paymentsService;
        private readonly IEmailsNotSentService emailsNotSentService;
        private readonly ISiteCountsService siteCountsService;

        public DashboardController(
            IBookingsService bookingsService,
            IInquiriesService inquiriesService,
            IPaymentsService paymentsService,
            IEmailsNotSentService emailsNotSentService,
            ISiteCountsService siteCountsService)
        {
            this.bookingsService = bookingsService;
            this.inquiriesService = inquiriesService;
            this.paymentsService = paymentsService;
            this.emailsNotSentService = emailsNotSentService;
            this.siteCountsService = siteCountsService;
        }

        // heardAbout picks the period of the "How customers heard about us" table: "all" for all
        // time, anything else (or nothing) for the last 30 days. counts does the same for the
        // table of what visitors did on the website.
        public async Task<IActionResult> Index(string heardAbout = null, string counts = null)
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
                SiteCounts = await this.SiteCountsAsync(counts == IndexViewModel.SiteCountsAllTime),
            };

            return this.View(viewModel);
        }

        // The button under the table of what visitors did: whether this browser's own looks at
        // the public site are counted. A browser that has opened the admin panel is not
        // (AdministrationController), and this is how one is put back, to try the count out.
        [HttpPost]
        public IActionResult CountThisBrowser(bool count)
        {
            SiteCounting.MarkBrowser(this.HttpContext, count);
            return this.RedirectToAction(nameof(this.Index), null, null, "dashboard-site-counts");
        }

        private async Task<SiteCountsViewModel> SiteCountsAsync(bool allTime)
        {
            DateOnly today = DateOnly.FromDateTime(UkTime.FromUtc(DateTime.UtcNow));
            DateOnly? firstDay = await this.siteCountsService.GetFirstDayAsync();
            DateOnly? periodStart = allTime ? null : today.AddDays(1 - IndexViewModel.SiteCountsDays);

            // The bookings, deposits and enquiries are counted from the later of the two days:
            // the start of the period, and the day the count began. Earlier ones have no visits
            // beside them to be read against. Before anything is counted, that day is today.
            DateOnly recordsFrom = firstDay ?? today;
            if (periodStart.HasValue && periodStart.Value > recordsFrom)
            {
                recordsFrom = periodStart.Value;
            }

            DateTime recordsSince = UkTime.ToUtc(recordsFrom.ToDateTime(TimeOnly.MinValue));

            SiteCountsViewModel siteCounts = SiteCountsViewModel.From(
                await this.siteCountsService.GetTotalsAsync(periodStart),
                await this.bookingsService.GetWebsiteBookingCountsAsync(recordsSince),
                await this.inquiriesService.GetCountSinceAsync(recordsSince));
            siteCounts.IsAllTime = allTime;
            siteCounts.CountingSince = firstDay;
            siteCounts.ThisBrowserIsCounted = this.Request.Cookies[SiteCounting.BrowserCookie] == SiteCounting.Counted;

            return siteCounts;
        }
    }
}
