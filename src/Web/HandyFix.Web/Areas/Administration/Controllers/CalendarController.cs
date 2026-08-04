namespace HandyFix.Web.Areas.Administration.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using HandyFix.Data.Models;
    using HandyFix.Services.Data.Availability;
    using HandyFix.Web.ViewModels.Administration.Calendar;

    using Microsoft.AspNetCore.Mvc;

    public class CalendarController : AdministrationController
    {
        private readonly IAvailabilityService availabilityService;

        public CalendarController(IAvailabilityService availabilityService)
        {
            this.availabilityService = availabilityService;
        }

        public async Task<IActionResult> Index(DateTime? date)
        {
            DateTime targetDate = date ?? DateTime.Today;

            // Deliberately does not generate: simply viewing a day must not create capacity for
            // it. Slots exist only when an admin generates them below (or the non-production
            // capacity seeder does). An empty day renders the "no slots" empty state instead.
            IEnumerable<AvailabilitySlot> slots = await this.availabilityService.GetAllSlotsForDayAsync(targetDate);

            var model = new CalendarIndexViewModel
            {
                TargetDate = targetDate,
                Slots = slots,
            };

            return this.View(model);
        }

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
            return this.RedirectToAction(nameof(this.Index), new { date = startDate });
        }

        [HttpPost]
        public async Task<IActionResult> BlockSlot(Guid id, DateTime returnDate)
        {
            await this.availabilityService.BlockSlotAsync(id);
            return this.RedirectToAction(nameof(this.Index), new { date = returnDate });
        }

        [HttpPost]
        public async Task<IActionResult> ReleaseSlot(Guid id, DateTime returnDate)
        {
            await this.availabilityService.ReleaseSlotAsync(id);
            return this.RedirectToAction(nameof(this.Index), new { date = returnDate });
        }

        [HttpPost]
        public async Task<IActionResult> BlockDate(DateTime date)
        {
            await this.availabilityService.BlockDateAsync(date);
            return this.RedirectToAction(nameof(this.Index), new { date });
        }
    }
}
