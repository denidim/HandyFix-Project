namespace HandyFix.Web.Areas.Administration.Controllers
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Data.Common.Repositories;
    using HandyFix.Data.Models;
    using HandyFix.Services.Data.Bookings;
    using HandyFix.Web.ViewModels.Booking;

    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;

    public class BookingsController : AdministrationController
    {
        private readonly IBookingsService bookingsService;
        private readonly IDeletableEntityRepository<Technician> technicianRepository;
        private readonly IDeletableEntityRepository<BookingStatus> statusRepository;

        public BookingsController(
            IBookingsService bookingsService,
            IDeletableEntityRepository<Technician> technicianRepository,
            IDeletableEntityRepository<BookingStatus> statusRepository)
        {
            this.bookingsService = bookingsService;
            this.technicianRepository = technicianRepository;
            this.statusRepository = statusRepository;
        }

        public async Task<IActionResult> Index(BookingSortField sortField = BookingSortField.CreatedOn, bool descending = true, string status = null)
        {
            var bookings = await this.bookingsService.GetAllBookingsAsync<BookingDetailsViewModel>(sortField, descending, status);
            var statusOptions = await this.statusRepository.All()
                .Select(x => x.Name)
                .OrderBy(x => x)
                .ToListAsync();

            // Summary cards always reflect the whole business, not just whatever
            // status filter is currently applied to the table below.
            var allBookings = string.IsNullOrWhiteSpace(status)
                ? bookings
                : await this.bookingsService.GetAllBookingsAsync<BookingDetailsViewModel>();

            var model = new BookingListViewModel
            {
                Bookings = bookings,
                StatusOptions = statusOptions,
                SortField = sortField,
                Descending = descending,
                StatusFilter = status,
                TodaysAppointmentsCount = allBookings.Count(b => b.ScheduledTime.Date == DateTime.Today),
                PendingApprovalCount = allBookings.Count(b => b.StatusName == "Pending"),
                MonthlyRevenue = allBookings.Any()
                    ? allBookings.Where(b => b.ScheduledTime.Month == DateTime.Today.Month && b.ScheduledTime.Year == DateTime.Today.Year).Sum(b => b.TotalAmount)
                    : 0,
            };

            return this.View(model);
        }

        public async Task<IActionResult> Details(Guid id)
        {
            var booking = await this.bookingsService.GetByIdAsync<BookingDetailsViewModel>(id);
            if (booking == null)
            {
                return this.NotFound();
            }

            var technicians = await this.technicianRepository.All().ToListAsync();
            booking.Technicians = technicians;

            return this.View(booking);
        }

        [HttpPost]
        public async Task<IActionResult> Approve(Guid id)
        {
            await this.bookingsService.UpdateStatusAsync(id, "Approved");
            return this.RedirectToAction(nameof(this.Details), new { id });
        }

        [HttpPost]
        public async Task<IActionResult> Complete(Guid id)
        {
            await this.bookingsService.UpdateStatusAsync(id, "Completed");
            return this.RedirectToAction(nameof(this.Details), new { id });
        }

        [HttpPost]
        public async Task<IActionResult> Cancel(Guid id)
        {
            await this.bookingsService.CancelBookingAsync(id);
            return this.RedirectToAction(nameof(this.Details), new { id });
        }

        [HttpPost]
        public async Task<IActionResult> AssignTechnician(Guid id, Guid technicianId)
        {
            await this.bookingsService.AssignTechnicianAsync(id, technicianId);
            return this.RedirectToAction(nameof(this.Details), new { id });
        }
    }
}
