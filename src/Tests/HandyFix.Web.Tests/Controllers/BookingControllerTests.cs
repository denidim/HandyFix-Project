namespace HandyFix.Web.Tests.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Data.Models;
    using HandyFix.Services;
    using HandyFix.Services.Data.Availability;
    using HandyFix.Services.Data.Bookings;
    using HandyFix.Services.Data.Services;
    using HandyFix.Web.Controllers;
    using HandyFix.Web.ViewModels.Booking;
    using HandyFix.Web.ViewModels.Services;

    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;

    using Moq;

    using Xunit;

    public class BookingControllerTests
    {
        // The exact promise made to the customer when someone else wins the slot race:
        // they lose the slot, not the twenty other fields they already filled in.
        private const string SlotTakenMessage = "This slot was just taken by someone else, please pick another.";

        public static TheoryData<Exception> SlotRaceExceptions => new TheoryData<Exception>
        {
            new SlotUnavailableException("Slot already booked."),
            new DbUpdateConcurrencyException("Row version mismatch."),
        };

        [Theory]
        [MemberData(nameof(SlotRaceExceptions))]
        public async Task IndexPostShouldRedisplayTheFormWhenTheSlotWasTakenMidSubmission(Exception slotRaceException)
        {
            var controller = BuildController(
                out var servicesService,
                out var availabilityService,
                out var bookingsService,
                out _);

            bookingsService
                .Setup(x => x.CreateBookingAsync(It.IsAny<BookingInputModel>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<string>()))
                .ThrowsAsync(slotRaceException);

            var model = ValidModel();

            var result = await controller.Index(model);

            // Must not fall through to Stripe: there is no booking to pay for.
            var viewResult = Assert.IsType<ViewResult>(result);
            var returnedModel = Assert.IsType<BookingInputModel>(viewResult.Model);

            Assert.Equal(SlotTakenMessage, SingleModelError(controller));

            // The slot is the one thing that must be cleared - it is gone, and
            // re-submitting it would just lose the race again.
            Assert.Equal(Guid.Empty, returnedModel.SlotId);

            // Everything else the customer typed has to survive.
            Assert.Equal("Ada", returnedModel.CustomerFirstName);
            Assert.Equal("Lovelace", returnedModel.CustomerLastName);
            Assert.Equal("ada@example.com", returnedModel.Email);
            Assert.Equal("07700900123", returnedModel.PhoneNumber);
            Assert.Equal("1 Analytical Engine Way, KT9 1AA", returnedModel.Address);
            Assert.Equal("The kitchen tap has been dripping for a week.", returnedModel.ProblemDescription);

            // And the form has to be usable again, which means its dropdowns get repopulated.
            Assert.Single(returnedModel.Services);
            Assert.Single(returnedModel.AvailableDates);
            servicesService.Verify(x => x.GetAllAsync<ServiceViewModel>(It.IsAny<bool>()), Times.Once);
            availabilityService.Verify(x => x.GetAvailableDatesAsync(It.IsAny<int>()), Times.Once);
        }

        [Fact]
        public async Task IndexPostShouldRedirectToPaymentWhenTheBookingIsCreated()
        {
            var controller = BuildController(out _, out _, out var bookingsService, out var imageService);

            var booking = new Booking();
            bookingsService
                .Setup(x => x.CreateBookingAsync(It.IsAny<BookingInputModel>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<string>()))
                .ReturnsAsync(booking);

            var result = await controller.Index(ValidModel());

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Pay", redirect.ActionName);
            Assert.Equal("Payment", redirect.ControllerName);
            Assert.Equal(booking.Id, redirect.RouteValues["bookingId"]);

            // Uploaded photos are pushed to storage before the booking is created,
            // so a failed upload can never leave a booking pointing at nothing.
            imageService.Verify(x => x.UploadImagesAsync(It.IsAny<IEnumerable<IFormFile>>(), "bookings"), Times.Once);
        }

        [Fact]
        public async Task IndexPostShouldNotCreateABookingWhenTheModelIsInvalid()
        {
            var controller = BuildController(out _, out _, out var bookingsService, out var imageService);
            controller.ModelState.AddModelError("Email", "Email address is required.");

            var result = await controller.Index(ValidModel());

            Assert.IsType<ViewResult>(result);
            bookingsService.Verify(
                x => x.CreateBookingAsync(It.IsAny<BookingInputModel>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<string>()),
                Times.Never);

            // No point paying Cloudflare to store photos for a booking that was never valid.
            imageService.Verify(x => x.UploadImagesAsync(It.IsAny<IEnumerable<IFormFile>>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task IndexPostShouldSurfaceTheServiceMessageForValidationFailures()
        {
            var controller = BuildController(out _, out _, out var bookingsService, out _);

            bookingsService
                .Setup(x => x.CreateBookingAsync(It.IsAny<BookingInputModel>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<string>()))
                .ThrowsAsync(new InvalidOperationException("That service is no longer offered."));

            var model = ValidModel();
            var result = await controller.Index(model);

            Assert.IsType<ViewResult>(result);

            // InvalidOperationException carries a message written for the customer,
            // so it is shown as-is rather than replaced with a generic apology.
            Assert.Equal("That service is no longer offered.", SingleModelError(controller));

            // ...and unlike the slot race, the slot itself is still fine - don't clear it.
            Assert.Equal(model.SlotId, ((BookingInputModel)((ViewResult)result).Model).SlotId);
        }

        [Fact]
        public async Task IndexPostShouldShowAGenericMessageForUnexpectedFailures()
        {
            var controller = BuildController(out _, out _, out var bookingsService, out _);

            // A database or storage failure must not leak its internals to the customer.
            bookingsService
                .Setup(x => x.CreateBookingAsync(It.IsAny<BookingInputModel>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<string>()))
                .ThrowsAsync(new TimeoutException("SqlException: connection forcibly closed by host 10.0.0.4"));

            var result = await controller.Index(ValidModel());

            Assert.IsType<ViewResult>(result);

            var error = SingleModelError(controller);
            Assert.Equal("An error occurred while saving your booking. Please try again.", error);
            Assert.DoesNotContain("10.0.0.4", error);
            Assert.DoesNotContain("SqlException", error);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("not-a-date")]
        [InlineData("2026-13-45")]
        public async Task GetSlotsShouldRejectUnparseableDates(string date)
        {
            var controller = BuildController(out _, out var availabilityService, out _, out _);

            var result = await controller.GetSlots(date);

            Assert.IsType<BadRequestObjectResult>(result);
            availabilityService.Verify(
                x => x.GetAllSlotsForDateAsync<AvailabilitySlotViewModel>(It.IsAny<DateTime>()),
                Times.Never);
        }

        [Fact]
        public async Task GetSlotsShouldReturnTheSlotsForAParseableDate()
        {
            var controller = BuildController(out _, out var availabilityService, out _, out _);

            var slots = new List<AvailabilitySlotViewModel> { new AvailabilitySlotViewModel() };
            availabilityService
                .Setup(x => x.GetAllSlotsForDateAsync<AvailabilitySlotViewModel>(new DateTime(2026, 8, 14)))
                .ReturnsAsync(slots);

            var result = await controller.GetSlots("2026-08-14");

            var json = Assert.IsType<JsonResult>(result);
            Assert.Same(slots, json.Value);
        }

        [Fact]
        public async Task GetSlotsShouldReturn500RatherThanThrowWhenAvailabilityLookupFails()
        {
            var controller = BuildController(out _, out var availabilityService, out _, out _);

            availabilityService
                .Setup(x => x.GetAllSlotsForDateAsync<AvailabilitySlotViewModel>(It.IsAny<DateTime>()))
                .ThrowsAsync(new InvalidOperationException("boom"));

            var result = await controller.GetSlots("2026-08-14");

            // The booking page fetches slots over AJAX; an unhandled exception here
            // would surface as an HTML error page inside a JSON parse.
            var statusResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, statusResult.StatusCode);
        }

        [Fact]
        public async Task ConfirmedShouldReturnNotFoundForAnUnknownBooking()
        {
            var controller = BuildController(out _, out _, out var bookingsService, out _);

            bookingsService
                .Setup(x => x.GetByIdAsync<BookingDetailsViewModel>(It.IsAny<Guid>()))
                .ReturnsAsync((BookingDetailsViewModel)null);

            var result = await controller.Confirmed(Guid.NewGuid());

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task ConfirmedShouldReturnTheBookingWhenItExists()
        {
            var controller = BuildController(out _, out _, out var bookingsService, out _);

            var booking = new BookingDetailsViewModel();
            bookingsService
                .Setup(x => x.GetByIdAsync<BookingDetailsViewModel>(It.IsAny<Guid>()))
                .ReturnsAsync(booking);

            var result = await controller.Confirmed(Guid.NewGuid());

            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Same(booking, viewResult.Model);
        }

        private static BookingInputModel ValidModel() => new BookingInputModel
        {
            CustomerFirstName = "Ada",
            CustomerLastName = "Lovelace",
            Email = "ada@example.com",
            PhoneNumber = "07700900123",
            Address = "1 Analytical Engine Way, KT9 1AA",
            ProblemDescription = "The kitchen tap has been dripping for a week.",
            SlotId = Guid.NewGuid(),
            ServiceId = Guid.NewGuid(),
        };

        private static string SingleModelError(Controller controller)
        {
            var errors = controller.ModelState
                .SelectMany(entry => entry.Value.Errors)
                .Select(error => error.ErrorMessage)
                .ToList();

            return Assert.Single(errors);
        }

        private static BookingController BuildController(
            out Mock<IServicesService> servicesService,
            out Mock<IAvailabilityService> availabilityService,
            out Mock<IBookingsService> bookingsService,
            out Mock<IImageService> imageService)
        {
            servicesService = new Mock<IServicesService>();
            availabilityService = new Mock<IAvailabilityService>();
            bookingsService = new Mock<IBookingsService>();
            imageService = new Mock<IImageService>();

            servicesService
                .Setup(x => x.GetAllAsync<ServiceViewModel>(It.IsAny<bool>()))
                .ReturnsAsync(new List<ServiceViewModel> { new ServiceViewModel() });

            availabilityService
                .Setup(x => x.GetAvailableDatesAsync(It.IsAny<int>()))
                .ReturnsAsync(new List<DateTime> { new DateTime(2026, 8, 14) });

            imageService
                .Setup(x => x.UploadImagesAsync(It.IsAny<IEnumerable<IFormFile>>(), It.IsAny<string>()))
                .ReturnsAsync(new List<string>());

            return new BookingController(
                servicesService.Object,
                availabilityService.Object,
                bookingsService.Object,
                imageService.Object);
        }
    }
}
