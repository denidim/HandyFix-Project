namespace HandyFix.Web.Areas.Administration.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Threading.Tasks;

    using HandyFix.Data.Models;
    using HandyFix.Services.Data.Availability;
    using HandyFix.Services.Data.Bookings;
    using HandyFix.Web.ViewModels.Administration.Calendar;
    using HandyFix.Web.ViewModels.Booking;

    using Microsoft.AspNetCore.Mvc;

    public class CalendarController : AdministrationController
    {
        private const string SuccessMessageKey = "SuccessMessage";
        private const string ErrorMessageKey = "ErrorMessage";

        private readonly IAvailabilityService availabilityService;
        private readonly IBookingsService bookingsService;

        public CalendarController(
            IAvailabilityService availabilityService,
            IBookingsService bookingsService)
        {
            this.availabilityService = availabilityService;
            this.bookingsService = bookingsService;
        }

        public async Task<IActionResult> Index(DateTime? date)
        {
            DateTime targetDate = date ?? DateTime.Today;

            // Deliberately does not generate: simply viewing a day must not create capacity for
            // it. Slots exist only when an admin generates them below (or the non-production
            // capacity seeder does). An empty day renders the "no slots" empty state instead.
            IEnumerable<AvailabilitySlot> slots = await this.availabilityService.GetAllSlotsForDayAsync(targetDate);

            // The day shows every job on it, written-in ones too: a job the admin wrote in
            // holds no slot, so the slots alone would show its hour as free
            // (PROJECT_STATE.md Section 3ce).
            IEnumerable<BookingDetailsViewModel> jobs = await this.bookingsService.GetJobsForDayAsync<BookingDetailsViewModel>(targetDate);

            var model = new CalendarIndexViewModel
            {
                TargetDate = targetDate,
                Slots = slots,
                Jobs = jobs,
            };

            return this.View(model);
        }

        // Each action below answers with a line saying what it did. They used to reload the
        // page in silence, and "Unblock" did nothing at all (PROJECT_STATE.md Section 3ce).
        [HttpPost]
        public async Task<IActionResult> GenerateSlots(DateTime startDate, DateTime endDate)
        {
            if (startDate < DateTime.Today)
            {
                startDate = DateTime.Today;
            }

            if (endDate < startDate)
            {
                endDate = startDate.AddDays(7);
            }

            await this.availabilityService.GenerateSlotsForRangeAsync(startDate, endDate);

            this.TempData[SuccessMessageKey] = $"Hourly slots are open from {Day(startDate)} to {Day(endDate)}, Sundays left out. A day that already had slots was left as it was.";
            return this.RedirectToAction(nameof(this.Index), new { date = startDate });
        }

        [HttpPost]
        public async Task<IActionResult> BlockSlot(Guid id, DateTime returnDate)
        {
            var blocked = await this.availabilityService.BlockSlotAsync(id);

            this.TempData[blocked ? SuccessMessageKey : ErrorMessageKey] = blocked
                ? "The hour is blocked. It cannot be booked on the website."
                : "Nothing was changed: that hour is no longer in the calendar.";

            return this.RedirectToAction(nameof(this.Index), new { date = returnDate });
        }

        [HttpPost]
        public async Task<IActionResult> UnblockSlot(Guid id, DateTime returnDate)
        {
            var opened = await this.availabilityService.UnblockSlotAsync(id);

            this.TempData[opened ? SuccessMessageKey : ErrorMessageKey] = opened
                ? "The hour is open again. It can be booked on the website."
                : "Nothing was changed: the calendar changed in the same moment. Please try again.";

            return this.RedirectToAction(nameof(this.Index), new { date = returnDate });
        }

        [HttpPost]
        public async Task<IActionResult> ReleaseSlot(Guid id, DateTime returnDate)
        {
            var released = await this.availabilityService.ReleaseSlotAsync(id);

            this.TempData[released ? SuccessMessageKey : ErrorMessageKey] = released
                ? "The hour is back on sale on the website. The job that held it keeps its day and time: open the job if it should be moved or cancelled."
                : "Nothing was changed: the calendar changed in the same moment. Please try again.";

            return this.RedirectToAction(nameof(this.Index), new { date = returnDate });
        }

        [HttpPost]
        public async Task<IActionResult> BlockDate(DateTime date)
        {
            await this.availabilityService.BlockDateAsync(date);

            this.TempData[SuccessMessageKey] = $"Every hour of {Day(date)} is blocked. Nothing on that day can be booked on the website. An hour is opened again with its own Unblock button.";
            return this.RedirectToAction(nameof(this.Index), new { date });
        }

        private static string Day(DateTime date)
        {
            return date.ToString("dddd d MMMM yyyy", CultureInfo.InvariantCulture);
        }
    }
}
