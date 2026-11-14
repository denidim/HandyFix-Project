namespace HandyFix.Web.ViewModels.Administration.Dashboard
{
    using System;
    using System.Collections.Generic;

    using HandyFix.Data.Models;
    using HandyFix.Web.ViewModels.Booking;

    // The Dashboard's table of what visitors did on the website (PROJECT_STATE.md Section 3fa).
    // The visits, the opened booking page, the picked times and the taps on Call and WhatsApp
    // are the sums the pages' script sends in; the bookings, the deposits and the enquiries are
    // the ones kept in the panel, counted from the day the count began so that the rows can be
    // read against each other.
    public class SiteCountsViewModel
    {
        // Whether the table counts from the day the count began rather than the last 30 days.
        public bool IsAllTime { get; set; }

        // The first day anything was counted; nothing until then.
        public DateOnly? CountingSince { get; set; }

        public int VisitsOnPhone { get; set; }

        public int VisitsOnComputer { get; set; }

        public int Visits => this.VisitsOnPhone + this.VisitsOnComputer;

        public int BookingOpened { get; set; }

        public int TimePicked { get; set; }

        public int BookingsSent { get; set; }

        public int DepositsPaid { get; set; }

        public int CallTapped { get; set; }

        public int WhatsAppTapped { get; set; }

        public int EnquiriesSent { get; set; }

        // Whether this browser's own looks at the public site are counted. A browser that has
        // opened the admin panel is not, unless the button under the table said otherwise.
        public bool ThisBrowserIsCounted { get; set; }

        public static SiteCountsViewModel From(
            IReadOnlyDictionary<SiteCountKind, int> totals,
            WebsiteBookingCounts bookings,
            int enquiries)
        {
            return new SiteCountsViewModel
            {
                VisitsOnPhone = totals.GetValueOrDefault(SiteCountKind.VisitOnPhone),
                VisitsOnComputer = totals.GetValueOrDefault(SiteCountKind.VisitOnComputer),
                BookingOpened = totals.GetValueOrDefault(SiteCountKind.BookingOpened),
                TimePicked = totals.GetValueOrDefault(SiteCountKind.TimePicked),
                BookingsSent = bookings.Sent,
                DepositsPaid = bookings.DepositPaid,
                CallTapped = totals.GetValueOrDefault(SiteCountKind.CallTapped),
                WhatsAppTapped = totals.GetValueOrDefault(SiteCountKind.WhatsAppTapped),
                EnquiriesSent = enquiries,
            };
        }
    }
}
