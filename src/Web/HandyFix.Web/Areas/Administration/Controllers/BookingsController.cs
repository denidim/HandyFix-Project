namespace HandyFix.Web.Areas.Administration.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Data.Models;
    using HandyFix.Services.Data.Bookings;
    using HandyFix.Services.Data.Inquiries;
    using HandyFix.Services.Data.Payments;
    using HandyFix.Services.Data.Services;
    using HandyFix.Services.Data.Technicians;
    using HandyFix.Web.ViewModels.Administration.Enquiries;
    using HandyFix.Web.ViewModels.Administration.Technicians;
    using HandyFix.Web.ViewModels.Booking;
    using HandyFix.Web.ViewModels.Services;

    using Microsoft.AspNetCore.Mvc;

    // Every job the business does has a page here, whichever way it arrived: booked on the
    // website with a deposit, or written in by the admin (PROJECT_STATE.md Section 3ce). The
    // address stays /Administration/Bookings; what the admin reads on the pages is "Jobs".
    public class BookingsController : AdministrationController
    {
        private const string SuccessMessageKey = "SuccessMessage";
        private const string ErrorMessageKey = "ErrorMessage";

        private readonly IBookingsService bookingsService;
        private readonly ITechniciansService techniciansService;
        private readonly IPaymentsService paymentsService;
        private readonly IServicesService servicesService;
        private readonly IInquiriesService inquiriesService;

        public BookingsController(
            IBookingsService bookingsService,
            ITechniciansService techniciansService,
            IPaymentsService paymentsService,
            IServicesService servicesService,
            IInquiriesService inquiriesService)
        {
            this.bookingsService = bookingsService;
            this.techniciansService = techniciansService;
            this.paymentsService = paymentsService;
            this.servicesService = servicesService;
            this.inquiriesService = inquiriesService;
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
            booking.Payments = await this.paymentsService.GetMoneyListAsync<PaymentLineViewModel>(id);
            booking.History = await this.bookingsService.GetHistoryAsync<BookingHistoryViewModel>(id);

            return this.View(booking);
        }

        // The form for a job that did not come through the website. Opened from an enquiry it
        // starts with what the enquiry holds; opened from a day in the calendar, with that day.
        [HttpGet]
        public async Task<IActionResult> Create(Guid? enquiryId = null, DateTime? date = null)
        {
            var model = new JobInputModel
            {
                Date = date?.Date,
                Source = BookingSource.Phone,
            };

            if (enquiryId.HasValue)
            {
                EnquiryViewModel enquiry = await this.inquiriesService.GetByIdAsync<EnquiryViewModel>(enquiryId.Value);
                if (enquiry == null)
                {
                    return this.NotFound();
                }

                // An enquiry has one name box. The first word is taken as the first name and
                // the rest as the last name, which the admin can put right in the form.
                var name = (enquiry.Name ?? string.Empty).Trim();
                var space = name.IndexOf(' ');

                model.EnquiryId = enquiry.Id;
                model.CustomerFirstName = space < 0 ? name : name.Substring(0, space);
                model.CustomerLastName = space < 0 ? null : name.Substring(space + 1).Trim();
                model.PhoneNumber = enquiry.PhoneNumber;
                model.Email = enquiry.Email;
                model.ProblemDescription = enquiry.Message;
                model.Source = BookingSource.Enquiry;
            }

            return await this.JobForm(model);
        }

        [HttpPost]
        public async Task<IActionResult> Create(JobInputModel model)
        {
            // The website is not on the form's list. It is refused here too, for a form sent
            // without the page: a "website" job with no deposit would sit in the list looking
            // like a booking that had been paid for.
            if (model.Source == BookingSource.Website)
            {
                this.ModelState.AddModelError(nameof(model.Source), "Pick where the job came from.");
            }

            if (!this.ModelState.IsValid)
            {
                return await this.JobForm(model);
            }

            Guid id = await this.bookingsService.CreateWrittenInJobAsync(model);

            this.TempData[SuccessMessageKey] = "The job is written in. Its hour is still on sale on the website: block it in the calendar if nobody else should be booked then. No email was sent to the customer.";
            return this.RedirectToAction(nameof(this.Details), new { id });
        }

        // There is no Approve action. A website booking is approved by its deposit being paid
        // (PaymentsService), and the email that used to hang off an admin's approval goes out
        // when a technician is picked. Each action below answers with a line saying what it did:
        // they used to reload the page in silence (PROJECT_STATE.md Section 3ce).
        [HttpPost]
        public async Task<IActionResult> Complete(Guid id, decimal? finalPrice)
        {
            if (finalPrice == null || finalPrice <= 0m)
            {
                this.TempData[ErrorMessageKey] = "Nothing was changed. Type the final price, more than £0, to mark the job as done.";
                return this.RedirectToAction(nameof(this.Details), new { id });
            }

            var completed = await this.bookingsService.CompleteBookingAsync(id, finalPrice.Value);

            this.TempData[completed ? SuccessMessageKey : ErrorMessageKey] = completed
                ? $"The job is marked as done, at a final price of {Pounds(finalPrice.Value)}."
                : "Nothing was changed. A job can be marked as done while it is booked, and a website booking only once its deposit is paid.";

            return this.RedirectToAction(nameof(this.Details), new { id });
        }

        [HttpPost]
        public async Task<IActionResult> FinalPrice(Guid id, decimal? finalPrice)
        {
            var changed = finalPrice > 0m && await this.bookingsService.ChangeFinalPriceAsync(id, finalPrice.Value);

            this.TempData[changed ? SuccessMessageKey : ErrorMessageKey] = changed
                ? $"The final price is now {Pounds(finalPrice.Value)}."
                : "Nothing was changed. The final price can be put right on a job that is done, and has to be more than £0.";

            return this.RedirectToAction(nameof(this.Details), new { id });
        }

        [HttpPost]
        public async Task<IActionResult> Cancel(Guid id, string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                this.TempData[ErrorMessageKey] = "Nothing was changed. Write the reason for cancelling: it is kept on the job.";
                return this.RedirectToAction(nameof(this.Details), new { id });
            }

            var cancelled = await this.bookingsService.CancelBookingAsync(id, reason);

            this.TempData[cancelled ? SuccessMessageKey : ErrorMessageKey] = cancelled
                ? "The job is cancelled. It keeps its date and the reason, and an hour it held in the calendar can be booked again. The customer has not been emailed: please tell them yourself. A deposit that goes back is refunded by hand in Stripe, then ticked here as refunded."
                : "Nothing was changed. This job is already done, cancelled or abandoned.";

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
                    this.TempData[SuccessMessageKey] = $"{result.TechnicianName} is now the technician for this job. The customer has been emailed the name and phone number.";
                    break;

                case TechnicianAssignmentOutcome.AssignedButEmailNotSent:
                    this.TempData[ErrorMessageKey] = $"{result.TechnicianName} is now the technician for this job, but the email to the customer could not be sent. Please give them the name and phone number yourself.";
                    break;

                case TechnicianAssignmentOutcome.AssignedNoEmailForWrittenInJob:
                    this.TempData[SuccessMessageKey] = $"{result.TechnicianName} is now the technician for this job. No email goes out for a job that was written in: please tell the customer yourself.";
                    break;

                case TechnicianAssignmentOutcome.Cleared:
                    this.TempData[SuccessMessageKey] = "This job has no technician now. The customer has not been emailed about it.";
                    break;

                case TechnicianAssignmentOutcome.Unchanged:
                    this.TempData[SuccessMessageKey] = result.TechnicianName != null
                        ? $"Nothing was changed: {result.TechnicianName} was already the technician. No email was sent."
                        : "Nothing was changed: this job had no technician, and none was picked.";
                    break;

                case TechnicianAssignmentOutcome.TechnicianNotFound:
                    this.TempData[ErrorMessageKey] = "Nothing was changed. That technician is no longer on the roster: pick another.";
                    break;

                default:
                    this.TempData[ErrorMessageKey] = "Nothing was changed. A technician can be picked while the job is booked, and on a website booking only once its deposit is paid.";
                    break;
            }

            return this.RedirectToAction(nameof(this.Details), new { id });
        }

        [HttpPost]
        public async Task<IActionResult> Move(Guid id, DateTime? date, TimeSpan? time)
        {
            if (date == null || time == null)
            {
                this.TempData[ErrorMessageKey] = "Nothing was changed. Pick both the new day and the new time.";
                return this.RedirectToAction(nameof(this.Details), new { id });
            }

            JobMoveResult result = await this.bookingsService.MoveBookingAsync(id, date.Value.Date + time.Value);

            switch (result.Outcome)
            {
                case JobMoveOutcome.BookingNotFound:
                    return this.NotFound();

                case JobMoveOutcome.Moved:
                    this.TempData[SuccessMessageKey] = MovedMessage(result);
                    break;

                case JobMoveOutcome.Unchanged:
                    this.TempData[SuccessMessageKey] = "Nothing was changed: the job is already at that day and time.";
                    break;

                case JobMoveOutcome.CalendarChanged:
                    this.TempData[ErrorMessageKey] = "Nothing was changed: the calendar changed in the same moment. Please try again.";
                    break;

                default:
                    this.TempData[ErrorMessageKey] = "Nothing was changed. A job can be moved while it is booked, and a website booking only once its deposit is paid.";
                    break;
            }

            return this.RedirectToAction(nameof(this.Details), new { id });
        }

        [HttpPost]
        public async Task<IActionResult> AddPayment(Guid id, decimal? amount, string method)
        {
            var added = amount > 0m && await this.paymentsService.AddPaymentAsync(id, amount.Value, method);

            this.TempData[added ? SuccessMessageKey : ErrorMessageKey] = added
                ? $"{Pounds(amount.Value)} is written on the job, paid by {method.ToLowerInvariant()}."
                : "Nothing was changed. A payment can be written on a job that is booked or done: type an amount above £0 and pick how it was paid.";

            return this.RedirectToAction(nameof(this.Details), new { id });
        }

        [HttpPost]
        public async Task<IActionResult> RemovePayment(Guid id, Guid paymentId)
        {
            var removed = await this.paymentsService.RemovePaymentAsync(id, paymentId);

            this.TempData[removed ? SuccessMessageKey : ErrorMessageKey] = removed
                ? "The payment is taken off the job."
                : "Nothing was changed. Only a payment that was written on the job by hand can be taken off.";

            return this.RedirectToAction(nameof(this.Details), new { id });
        }

        [HttpPost]
        public async Task<IActionResult> DepositRefunded(Guid id, bool refunded)
        {
            var saved = await this.paymentsService.SetDepositRefundedAsync(id, refunded);

            this.TempData[saved ? SuccessMessageKey : ErrorMessageKey] = !saved
                ? "Nothing was changed. Only the deposit of a cancelled job can be marked as refunded."
                : refunded
                    ? "The deposit is marked as refunded. It no longer counts as money in. The refund itself is made by hand in Stripe."
                    : "The deposit is marked as not refunded.";

            return this.RedirectToAction(nameof(this.Details), new { id });
        }

        [HttpPost]
        public async Task<IActionResult> Notes(Guid id, string notes)
        {
            if (!await this.bookingsService.SaveNotesAsync(id, notes))
            {
                return this.NotFound();
            }

            this.TempData[SuccessMessageKey] = "The notes are saved. Only the admin sees them.";
            return this.RedirectToAction(nameof(this.Details), new { id });
        }

        // What a move did to the calendar, in the line the admin is left with. The site sends
        // no email when a job is moved, so the line ends by saying who still has to be told.
        private static string MovedMessage(JobMoveResult result)
        {
            var parts = new List<string>
            {
                $"The job is moved to {result.NewStart.ToString("dddd d MMMM yyyy 'at' HH:mm", CultureInfo.InvariantCulture)}.",
            };

            if (result.OldHourFreed)
            {
                parts.Add("The hour it had is back on sale.");
            }

            if (result.NewHourTaken)
            {
                parts.Add("The new hour is taken off sale.");
            }
            else if (result.NewHourNotAvailable)
            {
                parts.Add("The new hour is not free in the calendar, so nothing was taken off sale: check that day in the calendar.");
            }
            else
            {
                parts.Add("Nothing was changed in the calendar: block the new hour by hand if nobody else should be booked then.");
            }

            parts.Add("The customer has not been emailed: please tell them yourself.");

            return string.Join(" ", parts);
        }

        private static string Pounds(decimal amount)
        {
            return "£" + amount.ToString("0.00", CultureInfo.InvariantCulture);
        }

        private async Task<IActionResult> JobForm(JobInputModel model)
        {
            // Building work is quoted, not booked by the hour, on the website. Written in by
            // hand any service can be picked, so the whole list is offered.
            IEnumerable<ServiceViewModel> services = await this.servicesService.GetAllAsync<ServiceViewModel>();
            model.Services = services.OrderBy(x => x.CategoryName).ThenBy(x => x.Name).ToList();

            return this.View("Create", model);
        }
    }
}
