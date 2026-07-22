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

        public BookingsController(IBookingsService bookingsService, IDeletableEntityRepository<Technician> technicianRepository)
        {
            this.bookingsService = bookingsService;
            this.technicianRepository = technicianRepository;
        }

        public async Task<IActionResult> Index()
        {
            var bookings = await this.bookingsService.GetAllBookingsAsync<BookingDetailsViewModel>();
            return this.View(bookings);
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
