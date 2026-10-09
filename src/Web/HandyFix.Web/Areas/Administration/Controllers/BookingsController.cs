namespace HandyFix.Web.Areas.Administration.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using HandyFix.Services.Data.Bookings;
    using HandyFix.Services.Data.Technicians;
    using HandyFix.Web.ViewModels.Administration.Technicians;
    using HandyFix.Web.ViewModels.Booking;

    using Microsoft.AspNetCore.Mvc;

    public class BookingsController : AdministrationController
    {
        private const string SuccessMessageKey = "SuccessMessage";
        private const string ErrorMessageKey = "ErrorMessage";

        private readonly IBookingsService bookingsService;
        private readonly ITechniciansService techniciansService;

        public BookingsController(
            IBookingsService bookingsService,
            ITechniciansService techniciansService)
        {
            this.bookingsService = bookingsService;
            this.techniciansService = techniciansService;
        }

        public async Task<IActionResult> Index(BookingSortField sortField = BookingSortField.CreatedOn, bool descending = true, string status = null)
        {
            IEnumerable<BookingDetailsViewModel> bookings = await this.bookingsService.GetAllBookingsAsync<BookingDetailsViewModel>(sortField, descending, status);
            IEnumerable<string> statusOptions = await this.bookingsService.GetStatusOptionsAsync();

            // Summary cards always reflect the whole business, not just whatever
            // status filter is currently applied to the table below.
            IEnumerable<BookingDetailsViewModel> allBookings = string.IsNullOrWhiteSpace(status)
                ? bookings
                : await this.bookingsService.GetAllBookingsAsync<BookingDetailsViewModel>();

            BookingSummaryStats summary = this.bookingsService.GetSummaryStats(allBookings);

            var model = new BookingListViewModel
            {
                Bookings = bookings,
                StatusOptions = statusOptions,
                SortField = sortField,
                Descending = descending,
                StatusFilter = status,
                TodaysAppointmentsCount = summary.TodaysAppointmentsCount,
                AwaitingTechnicianCount = summary.AwaitingTechnicianCount,
                MonthlyRevenue = summary.MonthlyRevenue,
            };

            return this.View(model);
        }

        public async Task<IActionResult> Details(Guid id)
        {
            BookingDetailsViewModel booking = await this.bookingsService.GetByIdAsync<BookingDetailsViewModel>(id);
            if (booking == null)
            {
                return this.NotFound();
            }

            booking.Technicians = await this.techniciansService
                .GetAssignableAsync<TechnicianOptionViewModel>(booking.TechnicianId);

            return this.View(booking);
        }

        // There is no Approve action. A booking is approved by its deposit being paid
        // (PaymentsService), and the email that used to hang off an admin's approval goes out
        // when a technician is picked. Each action below answers with a line saying what it did:
        // they used to reload the page in silence (PROJECT_STATE.md Section 3ce).
        [HttpPost]
        public async Task<IActionResult> Complete(Guid id)
        {
            var completed = await this.bookingsService.CompleteBookingAsync(id);

            this.TempData[completed ? SuccessMessageKey : ErrorMessageKey] = completed
                ? "The booking is marked as completed."
                : "Nothing was changed. A booking can be marked as completed once its deposit is paid, and only while it is not already completed or cancelled.";

            return this.RedirectToAction(nameof(this.Details), new { id });
        }

        [HttpPost]
        public async Task<IActionResult> Cancel(Guid id)
        {
            var cancelled = await this.bookingsService.CancelBookingAsync(id);

            this.TempData[cancelled ? SuccessMessageKey : ErrorMessageKey] = cancelled
                ? "The booking is cancelled and its time slot can be booked again. The customer has not been emailed: please tell them yourself. A deposit that goes back is refunded by hand in Stripe."
                : "Nothing was changed. This booking is already completed, cancelled or abandoned.";

            return this.RedirectToAction(nameof(this.Details), new { id });
        }

        [HttpPost]
        public async Task<IActionResult> AssignTechnician(Guid id, Guid? technicianId)
        {
            // Nullable on purpose: the picker's blank "-- Unassigned --" option posts an empty
            // value, which used to bind to Guid.Empty and then fail the TechnicianId foreign key
            // at SaveChanges. It now clears the assignment, which is what the option says it does.
            TechnicianAssignmentResult result = await this.bookingsService.AssignTechnicianAsync(id, technicianId);

            switch (result.Outcome)
            {
                case TechnicianAssignmentOutcome.BookingNotFound:
                    return this.NotFound();

                case TechnicianAssignmentOutcome.AssignedAndCustomerEmailed:
                    this.TempData[SuccessMessageKey] = $"{result.TechnicianName} is now the technician for this booking. The customer has been emailed the name and phone number.";
                    break;

                case TechnicianAssignmentOutcome.AssignedButEmailNotSent:
                    this.TempData[ErrorMessageKey] = $"{result.TechnicianName} is now the technician for this booking, but the email to the customer could not be sent. Please give them the name and phone number yourself.";
                    break;

                case TechnicianAssignmentOutcome.Cleared:
                    this.TempData[SuccessMessageKey] = "This booking has no technician now. The customer has not been emailed about it.";
                    break;

                case TechnicianAssignmentOutcome.Unchanged:
                    this.TempData[SuccessMessageKey] = result.TechnicianName != null
                        ? $"Nothing was changed: {result.TechnicianName} was already the technician. No email was sent."
                        : "Nothing was changed: this booking had no technician, and none was picked.";
                    break;

                case TechnicianAssignmentOutcome.TechnicianNotFound:
                    this.TempData[ErrorMessageKey] = "Nothing was changed. That technician is no longer on the roster: pick another.";
                    break;

                default:
                    this.TempData[ErrorMessageKey] = "Nothing was changed. A technician can be picked once the deposit is paid, and only while the booking is not completed or cancelled.";
                    break;
            }

            return this.RedirectToAction(nameof(this.Details), new { id });
        }
    }
}
