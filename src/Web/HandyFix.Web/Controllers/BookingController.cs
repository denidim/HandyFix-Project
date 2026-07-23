namespace HandyFix.Web.Controllers
{
    using System;
    using System.Threading.Tasks;

    using HandyFix.Services;
    using HandyFix.Services.Data.Availability;
    using HandyFix.Services.Data.Bookings;
    using HandyFix.Services.Data.Services;
    using HandyFix.Web.ViewModels.Booking;
    using HandyFix.Web.ViewModels.Services;

    using Microsoft.AspNetCore.Mvc;

    public class BookingController : BaseController
    {
        private readonly IServicesService servicesService;
        private readonly IAvailabilityService availabilityService;
        private readonly IBookingsService bookingsService;
        private readonly IImageService imageService;

        public BookingController(
            IServicesService servicesService,
            IAvailabilityService availabilityService,
            IBookingsService bookingsService,
            IImageService imageService)
        {
            this.servicesService = servicesService;
            this.availabilityService = availabilityService;
            this.bookingsService = bookingsService;
            this.imageService = imageService;
        }

        [HttpGet]
        [Route("Booking")]
        public async Task<IActionResult> Index(Guid? serviceId)
        {
            try
            {
                var services = await this.servicesService.GetAllAsync<ServiceViewModel>();
                var dates = await this.availabilityService.GetAvailableDatesAsync();

                var model = new BookingInputModel
                {
                    Services = services,
                    AvailableDates = dates,
                    SelectedServiceId = serviceId,
                };

                return this.View(model);
            }
            catch (Exception)
            {
                return this.View("Error");
            }
        }

        [HttpGet]
        [Route("Booking/GetSlots")]
        public async Task<IActionResult> GetSlots(string date)
        {
            if (string.IsNullOrWhiteSpace(date) || !DateTime.TryParse(date, out var parsedDate))
            {
                return this.BadRequest("Invalid date format.");
            }

            try
            {
                var slots = await this.availabilityService.GetAllSlotsForDateAsync<AvailabilitySlotViewModel>(parsedDate);
                return this.Json(slots);
            }
            catch (Exception)
            {
                return this.StatusCode(500, new { message = "An error occurred while loading time slots." });
            }
        }

        [HttpPost]
        [Route("Booking")]
        public async Task<IActionResult> Index(BookingInputModel model)
        {
            if (!this.ModelState.IsValid)
            {
                model.Services = await this.servicesService.GetAllAsync<ServiceViewModel>();
                model.AvailableDates = await this.availabilityService.GetAvailableDatesAsync();
                model.SelectedServiceId = model.ServiceId;

                return this.View(model);
            }

            try
            {
                var imageUrls = await this.imageService.UploadImagesAsync(model.Images, "bookings");
                var booking = await this.bookingsService.CreateBookingAsync(model, imageUrls);

                // Redirect to Stripe checkout
                return this.RedirectToAction("Pay", "Payment", new { bookingId = booking.Id });
            }
            catch (Exception)
            {
                this.ModelState.AddModelError(string.Empty, "An error occurred while saving your booking. Please try again.");

                model.Services = await this.servicesService.GetAllAsync<ServiceViewModel>();
                model.AvailableDates = await this.availabilityService.GetAvailableDatesAsync();
                model.SelectedServiceId = model.ServiceId;

                return this.View(model);
            }
        }

        [HttpGet]
        [Route("Booking/Confirmed/{id}")]
        public async Task<IActionResult> Confirmed(Guid id)
        {
            try
            {
                var booking = await this.bookingsService.GetByIdAsync<BookingDetailsViewModel>(id);
                if (booking == null)
                {
                    return this.NotFound();
                }

                return this.View(booking);
            }
            catch (Exception)
            {
                return this.View("Error");
            }
        }
    }
}
