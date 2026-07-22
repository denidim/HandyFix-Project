namespace HandyFix.Web.Areas.Administration.Controllers
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Data.Common.Repositories;
    using HandyFix.Data.Models;
    using HandyFix.Services.Data.Availability;
    using HandyFix.Web.ViewModels.Administration.Calendar;

    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;

    public class CalendarController : AdministrationController
    {
        private readonly IAvailabilityService availabilityService;
        private readonly IDeletableEntityRepository<AvailabilitySlot> slotRepository;

        public CalendarController(IAvailabilityService availabilityService, IDeletableEntityRepository<AvailabilitySlot> slotRepository)
        {
            this.availabilityService = availabilityService;
            this.slotRepository = slotRepository;
        }

        public async Task<IActionResult> Index(DateTime? date)
        {
            var targetDate = date ?? DateTime.Today;

            // Generate slots for today if not present
            await this.availabilityService.GenerateSlotsForRangeAsync(targetDate, targetDate);

            // Fetch all slots for this day, whether booked, active, or blocked
            var slots = await this.slotRepository.All()
                .Where(x => x.StartTime.Date == targetDate.Date)
                .OrderBy(x => x.StartTime)
                .ToListAsync();

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
