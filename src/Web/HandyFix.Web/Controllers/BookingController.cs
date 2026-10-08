namespace HandyFix.Web.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using HandyFix.Common;
    using HandyFix.Data.Models;
    using HandyFix.Services;
    using HandyFix.Services.Data.Availability;
    using HandyFix.Services.Data.Bookings;
    using HandyFix.Services.Data.ServiceAreas;
    using HandyFix.Services.Data.Services;
    using HandyFix.Web.Services.Forms;
    using HandyFix.Web.ViewModels;
    using HandyFix.Web.ViewModels.Booking;
    using HandyFix.Web.ViewModels.Services;
    using HandyFix.Web.ViewModels.Validation;

    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.RateLimiting;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;

    [AllowAnonymous]
    public class BookingController : BaseController
    {
        // Quote-only: the wizard has no tab for it and leaves its services out of the list.
        private const string QuoteOnlyCategorySlug = "small-building-works";

        private readonly IServicesService servicesService;
        private readonly IAvailabilityService availabilityService;
        private readonly IBookingsService bookingsService;
        private readonly IImageService imageService;
        private readonly IServiceAreasService serviceAreasService;
        private readonly IFormGuard formGuard;
        private readonly ILogger<BookingController> logger;

        public BookingController(
            IServicesService servicesService,
            IAvailabilityService availabilityService,
            IBookingsService bookingsService,
            IImageService imageService,
            IServiceAreasService serviceAreasService,
            IFormGuard formGuard,
            ILogger<BookingController> logger)
        {
            this.servicesService = servicesService;
            this.availabilityService = availabilityService;
            this.bookingsService = bookingsService;
            this.imageService = imageService;
            this.serviceAreasService = serviceAreasService;
            this.formGuard = formGuard;
            this.logger = logger;
        }

        [HttpGet]
        [Route("Booking")]
        public async Task<IActionResult> Index(string categorySlug = null, DateTime? date = null, Guid? selectedServiceId = null)
        {
            // A link asking to book the quote-only category goes to the Contact form with that
            // category chosen, which is where building work is quoted. The wizard has no tab for
            // it, and with no tab ticked its script stops (PROJECT_STATE Section 3bz).
            if (string.Equals(categorySlug, QuoteOnlyCategorySlug, StringComparison.OrdinalIgnoreCase))
            {
                return this.RedirectToAction("Contact", "Home", new { categorySlug = QuoteOnlyCategorySlug });
            }

            // The date and the service id are typed, so the model binder reads them, as it does for
            // the admin calendar. Whatever in the link is not a date or not an id arrives here as
            // null, and the page opens on today with no service chosen (PROJECT_STATE Section 3bw).
            try
            {
                IEnumerable<ServiceViewModel> services = await this.servicesService.GetAllAsync<ServiceViewModel>();
                IEnumerable<DateTime> dates = await this.availabilityService.GetAvailableDatesAsync();

                // A link that names a service but no category would open on the default Plumbing
                // tab, where a handyman service is not in the list and gets replaced by the
                // general plumbing one (PROJECT_STATE Section 3br). The service knows its category.
                if (string.IsNullOrEmpty(categorySlug) && selectedServiceId.HasValue)
                {
                    ServiceViewModel selected = services.FirstOrDefault(s => s.Id == selectedServiceId.Value);
                    if (selected != null && selected.CategorySlug != QuoteOnlyCategorySlug)
                    {
                        categorySlug = selected.CategorySlug;
                    }
                }

                var model = new BookingInputModel
                {
                    Services = services,
                    AvailableDates = dates,
                    SelectedCategorySlug = categorySlug,
                    SelectedDate = date,
                    SelectedServiceId = selectedServiceId,
                    ServedPostcodeDistricts = await this.serviceAreasService.GetServedPostcodeDistrictsAsync(),
                };

                this.ViewData["MetaDescription"] = "Book a plumbing or handyman appointment online across Surrey and South London. Pick a service, choose an available slot, and secure it with a deposit.";

                return this.View(model);
            }
            catch (Exception ex)
            {
                return this.SomethingWentWrong(ex);
            }
        }

        [HttpGet]
        [Route("Booking/GetSlots")]
        [EnableRateLimiting(RateLimits.SlotLookupsPolicy)]
        public async Task<IActionResult> GetSlots(string date)
        {
            if (string.IsNullOrWhiteSpace(date) || !DateTime.TryParse(date, out DateTime parsedDate))
            {
                return this.BadRequest("Invalid date format.");
            }

            try
            {
                IEnumerable<AvailabilitySlotViewModel> slots = await this.availabilityService.GetAllSlotsForDateAsync<AvailabilitySlotViewModel>(parsedDate);
                return this.Json(slots);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "The time slots for a date could not be loaded");
                return this.StatusCode(500, new { message = "An error occurred while loading time slots." });
            }
        }

        [HttpPost]
        [Route("Booking")]
        [EnableRateLimiting(RateLimits.FormsPolicy)]
        public async Task<IActionResult> Index(BookingInputModel model)
        {
            if (!this.ModelState.IsValid)
            {
                return await this.RedisplayBookingForm(model);
            }

            // A submission that carries a program's mark. The other forms answer one with their
            // usual thank-you; a booking's success is the payment page, which cannot be faked, so
            // this comes back with a message that says nothing about why and still gives a
            // person a way to book.
            FormGuardResult guard = await this.formGuard.CheckAsync(this.HttpContext, "booking");
            if (guard == FormGuardResult.Automated)
            {
                return await this.RedisplayBookingForm(
                    model,
                    $"We could not take this booking online. Please call us or message us on WhatsApp on {GlobalConstants.BusinessPhone}.");
            }

            // Before anything is saved or a deposit asked for: a postcode outside the districts
            // the service areas list is a job we may not be able to reach. The page says so as
            // the postcode is typed; this is the check a visitor cannot skip.
            if (!await this.serviceAreasService.IsPostcodeServedAsync(model.Postcode))
            {
                model.PostcodeNotServed = true;
                return await this.RedisplayBookingForm(
                    model,
                    $"Sorry, we don't take online bookings for {UkPostcode.GetOutwardCode(model.Postcode)} yet. Send us an enquiry or call {GlobalConstants.BusinessPhone}, and we'll tell you whether we can come out to you.");
            }

            IReadOnlyList<string> imageUrls;
            try
            {
                imageUrls = await this.imageService.UploadImagesAsync(model.Images, "bookings");
            }
            catch (ImageUploadValidationException ex)
            {
                // The customer's to put right: too many photos, one too large, not a picture.
                return await this.RedisplayBookingForm(model, ex.Message);
            }
            catch (Exception ex)
            {
                // Storage is out of reach, which is not the customer's to fix and not a reason to
                // lose a booking: it is taken without its photos. This used to stop the booking,
                // and a storage that was not set up showed its own error text to the customer
                // (PROJECT_STATE Section 3cb).
                this.logger.LogError(ex, "Booking photos could not be stored; taking the booking without them");
                imageUrls = Array.Empty<string>();
            }

            try
            {
                Booking booking = await this.bookingsService.CreateBookingAsync(model, imageUrls);

                // Redirect to Stripe checkout
                return this.RedirectToAction("Pay", "Payment", new { bookingId = booking.Id });
            }
            catch (Exception ex) when (ex is SlotUnavailableException || ex is DbUpdateConcurrencyException)
            {
                // Someone else claimed this slot between the customer picking it and
                // submitting the form. Clear it so they have to pick a fresh one, but
                // keep every other field they already filled in.
                model.SlotId = Guid.Empty;
                return await this.RedisplayBookingForm(model, "This slot was just taken by someone else, please pick another.");
            }
            catch (InvalidOperationException ex)
            {
                return await this.RedisplayBookingForm(model, ex.Message);
            }
            catch (Exception)
            {
                return await this.RedisplayBookingForm(model, "An error occurred while saving your booking. Please try again.");
            }
        }

        // The site's own "something went wrong" page, with the status that says so. These spots
        // used to show a leftover developer page and write nothing to the log, so a booking page
        // that failed left no trace of why (PROJECT_STATE Section 3cb).
        private IActionResult SomethingWentWrong(Exception ex)
        {
            this.logger.LogError(ex, "A booking page could not be shown");

            ViewResult page = this.View("StatusPage", StatusPageViewModel.For(StatusCodes.Status500InternalServerError));
            page.StatusCode = StatusCodes.Status500InternalServerError;
            return page;
        }

        private async Task<IActionResult> RedisplayBookingForm(BookingInputModel model, string errorMessage = null)
        {
            if (errorMessage != null)
            {
                this.ModelState.AddModelError(string.Empty, errorMessage);
            }

            model.Services = await this.servicesService.GetAllAsync<ServiceViewModel>();
            model.AvailableDates = await this.availabilityService.GetAvailableDatesAsync();
            model.ServedPostcodeDistricts = await this.serviceAreasService.GetServedPostcodeDistrictsAsync();
            model.SelectedServiceId = model.ServiceId;

            // The tabs are not part of what the form posts, so the category comes back from the
            // service. Without it the page reopened on Plumbing and swapped a handyman service
            // for the plumbing default, at the plumbing rate (PROJECT_STATE Section 3ca).
            model.SelectedCategorySlug = model.Services.FirstOrDefault(s => s.Id == model.ServiceId)?.CategorySlug;

            return this.View(model);
        }

        [HttpGet]
        [Route("Booking/Confirmed/{id}")]
        public async Task<IActionResult> Confirmed(Guid id)
        {
            try
            {
                BookingDetailsViewModel booking = await this.bookingsService.GetByIdAsync<BookingDetailsViewModel>(id);
                if (booking == null)
                {
                    return this.NotFound();
                }

                return this.View(booking);
            }
            catch (Exception ex)
            {
                return this.SomethingWentWrong(ex);
            }
        }
    }
}
