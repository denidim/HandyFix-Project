namespace HandyFix.Web.Controllers
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Services.Data.Availability;
    using HandyFix.Services.Data.Bookings;
    using HandyFix.Services.Data.Services;
    using HandyFix.Web.ViewModels.Booking;
    using HandyFix.Web.ViewModels.Services;

    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Mvc;

    public class BookingController : BaseController
    {
        private readonly IServicesService servicesService;
        private readonly IAvailabilityService availabilityService;
        private readonly IBookingsService bookingsService;
        private readonly IWebHostEnvironment environment;

        public BookingController(
            IServicesService servicesService,
            IAvailabilityService availabilityService,
            IBookingsService bookingsService,
            IWebHostEnvironment environment)
        {
            this.servicesService = servicesService;
            this.availabilityService = availabilityService;
            this.bookingsService = bookingsService;
            this.environment = environment;
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
                var slots = await this.availabilityService.GetAvailableSlotsForDateAsync<AvailabilitySlotViewModel>(parsedDate);
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
                // Map selected service
                var serviceIds = new[] { model.ServiceId };

                // Create booking
                var booking = await this.bookingsService.CreateBookingAsync(
                    model.CustomerFirstName,
                    model.CustomerLastName,
                    model.Email,
                    model.PhoneNumber,
                    model.Address,
                    model.ProblemDescription,
                    model.SlotId,
                    serviceIds);

                // Handle images upload
                if (model.Images != null && model.Images.Count > 0)
                {
                    var uploadsFolder = Path.Combine(this.environment.WebRootPath, "uploads", "bookings");
                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }

                    foreach (var image in model.Images)
                    {
                        if (image.Length > 0)
                        {
                            var uniqueFileName = $"{Guid.NewGuid()}_{Path.GetFileName(image.FileName)}";
                            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                            using (var fileStream = new FileStream(filePath, FileMode.Create))
                            {
                                await image.CopyToAsync(fileStream);
                            }

                            var imageUrl = $"/uploads/bookings/{uniqueFileName}";
                            await this.bookingsService.AddBookingImageAsync(booking.Id, imageUrl);
                        }
                    }
                }

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
