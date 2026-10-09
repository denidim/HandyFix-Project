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
    using HandyFix.Services.Data.ServiceAreas;
    using HandyFix.Services.Data.Services;
    using HandyFix.Web.Controllers;
    using HandyFix.Web.Services.Forms;
    using HandyFix.Web.ViewModels.Booking;
    using HandyFix.Web.ViewModels.Services;

    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging.Abstractions;

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
            Assert.Equal("1 Analytical Engine Way, Chessington", returnedModel.Address);
            Assert.Equal("KT9 1AA", returnedModel.Postcode);
            Assert.Equal("The kitchen tap has been dripping for a week.", returnedModel.ProblemDescription);

            // And the form has to be usable again, which means its dropdowns get repopulated.
            Assert.Single(returnedModel.Services);
            Assert.Single(returnedModel.AvailableDates);
            servicesService.Verify(x => x.GetAllAsync<ServiceViewModel>(It.IsAny<bool>()), Times.Once);
            availabilityService.Verify(x => x.GetAvailableDatesAsync(It.IsAny<int>()), Times.Once);
        }

        // A form that comes back used to open on today with no slot chosen, so the customer
        // picked the day and the time a second time. It opens on the day of the slot they had
        // (PROJECT_STATE Section 3cb). A slot lost to someone else is cleared first, and then
        // there is no day to go back to.
        [Fact]
        public async Task IndexPostShouldReopenTheFormOnTheDayOfTheSlotTheCustomerHadChosen()
        {
            var controller = BuildController(out _, out var availabilityService, out var bookingsService, out _);
            var model = ValidModel();
            availabilityService.Setup(x => x.GetSlotDateAsync(model.SlotId)).ReturnsAsync(new DateTime(2026, 10, 20));
            controller.ModelState.AddModelError(nameof(BookingInputModel.Email), "Please enter a full email address, for example name@example.com.");

            var result = await controller.Index(model);

            var returnedModel = Assert.IsType<BookingInputModel>(Assert.IsType<ViewResult>(result).Model);
            Assert.Equal(new DateTime(2026, 10, 20), returnedModel.SelectedDate);
            Assert.Equal(model.SlotId, returnedModel.SlotId);
        }

        [Fact]
        public async Task IndexPostShouldNotGoBackToTheDayOfASlotThatWasTaken()
        {
            var controller = BuildController(out _, out var availabilityService, out var bookingsService, out _);
            bookingsService
                .Setup(x => x.CreateBookingAsync(It.IsAny<BookingInputModel>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<string>()))
                .ThrowsAsync(new SlotUnavailableException("Slot already booked."));

            var result = await controller.Index(ValidModel());

            var returnedModel = Assert.IsType<BookingInputModel>(Assert.IsType<ViewResult>(result).Model);
            Assert.Equal(Guid.Empty, returnedModel.SlotId);
            Assert.Null(returnedModel.SelectedDate);
            availabilityService.Verify(x => x.GetSlotDateAsync(It.IsAny<Guid>()), Times.Never);
        }

        [Fact]
        public async Task IndexPostShouldReopenTheFormOnTheCategoryOfTheChosenService()
        {
            // The category tabs are not part of what the form posts. Without the category the page
            // reopened on Plumbing, where a handyman service is not in the list, and swapped it for
            // the plumbing default at the plumbing rate (PROJECT_STATE Section 3ca).
            var controller = BuildController(out var servicesService, out _, out _, out _);
            var gutterClearing = new ServiceViewModel { Id = Guid.NewGuid(), CategorySlug = "handyman" };
            servicesService
                .Setup(x => x.GetAllAsync<ServiceViewModel>(It.IsAny<bool>()))
                .ReturnsAsync(new List<ServiceViewModel> { new ServiceViewModel { Id = Guid.NewGuid(), CategorySlug = "plumbing" }, gutterClearing });
            controller.ModelState.AddModelError(nameof(BookingInputModel.Email), "The Email field is not a valid e-mail address.");

            var model = ValidModel();
            model.ServiceId = gutterClearing.Id;

            var result = await controller.Index(model);

            var returnedModel = Assert.IsType<BookingInputModel>(Assert.IsType<ViewResult>(result).Model);
            Assert.Equal("handyman", returnedModel.SelectedCategorySlug);
            Assert.Equal(gutterClearing.Id, returnedModel.SelectedServiceId);
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

        // A booking is a paid deposit for a visit. A postcode outside the districts the service
        // areas list is turned down before anything is saved, with the way forward: an enquiry
        // or a call (PROJECT_STATE Section 3cb).
        [Fact]
        public async Task IndexPostShouldTurnDownAPostcodeOutsideTheAreasWeCover()
        {
            var serviceAreasService = new Mock<IServiceAreasService>();
            serviceAreasService.Setup(x => x.IsPostcodeServedAsync("m1 1ae")).ReturnsAsync(false);
            var controller = BuildController(out _, out _, out var bookingsService, out var imageService, serviceAreasService);

            var model = ValidModel();
            model.Postcode = "m1 1ae";

            var result = await controller.Index(model);

            var returnedModel = Assert.IsType<BookingInputModel>(Assert.IsType<ViewResult>(result).Model);
            Assert.True(returnedModel.PostcodeNotServed);
            Assert.Equal(new[] { "KT9", "KT10" }, returnedModel.ServedPostcodeDistricts);

            var error = SingleModelError(controller);
            Assert.Contains("M1", error);
            Assert.Contains("enquiry", error);
            Assert.Contains(HandyFix.Common.GlobalConstants.BusinessPhone, error);

            // What the visitor typed and chose survives, slot included: nothing is wrong with it.
            Assert.Equal(model.SlotId, returnedModel.SlotId);
            Assert.Equal("Ada", returnedModel.CustomerFirstName);

            bookingsService.Verify(
                x => x.CreateBookingAsync(It.IsAny<BookingInputModel>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<string>()),
                Times.Never);
            imageService.Verify(x => x.UploadImagesAsync(It.IsAny<IEnumerable<IFormFile>>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task IndexGetShouldGiveThePageTheDistrictsWeCover()
        {
            var controller = BuildController(out _, out _, out _, out _);

            var result = await controller.Index(categorySlug: null, date: null, selectedServiceId: null);

            var model = Assert.IsType<BookingInputModel>(Assert.IsType<ViewResult>(result).Model);
            Assert.Equal(new[] { "KT9", "KT10" }, model.ServedPostcodeDistricts);
            Assert.False(model.PostcodeNotServed);
        }

        // A submission with a program's mark on it. The other forms answer one with their usual
        // thank-you; a booking's success is the payment page, which cannot be faked, so the form
        // comes back with a message that gives nothing away and still tells a person how to book
        // (PROJECT_STATE Section 3cb).
        [Fact]
        public async Task IndexPostShouldNotBookForAProgramAndSayNothingOfWhy()
        {
            var formGuard = new Mock<IFormGuard>();
            formGuard.Setup(g => g.CheckAsync(It.IsAny<HttpContext>(), "booking")).ReturnsAsync(FormGuardResult.Automated);
            var controller = BuildController(out _, out _, out var bookingsService, out var imageService, formGuard: formGuard);

            var result = await controller.Index(ValidModel());

            Assert.IsType<ViewResult>(result);
            var error = SingleModelError(controller);
            Assert.Contains("We could not take this booking online", error);
            Assert.Contains(HandyFix.Common.GlobalConstants.BusinessPhone, error);
            Assert.DoesNotContain("robot", error, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("automat", error, StringComparison.OrdinalIgnoreCase);

            bookingsService.Verify(
                x => x.CreateBookingAsync(It.IsAny<BookingInputModel>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<string>()),
                Times.Never);
            imageService.Verify(x => x.UploadImagesAsync(It.IsAny<IEnumerable<IFormFile>>(), It.IsAny<string>()), Times.Never);
        }

        // The "are you a person" check did not pass. A person can land here, so the form comes back
        // as filled in, slot included, with a message and a way round it.
        [Fact]
        public async Task IndexPostShouldComeBackWithAMessageWhenThePersonCheckDidNotPass()
        {
            var formGuard = new Mock<IFormGuard>();
            formGuard.Setup(g => g.CheckAsync(It.IsAny<HttpContext>(), "booking")).ReturnsAsync(FormGuardResult.ChallengeFailed);
            var controller = BuildController(out _, out _, out var bookingsService, out _, formGuard: formGuard);
            var model = ValidModel();

            var result = await controller.Index(model);

            var returnedModel = Assert.IsType<BookingInputModel>(Assert.IsType<ViewResult>(result).Model);
            Assert.Equal(model.SlotId, returnedModel.SlotId);
            var error = SingleModelError(controller);
            Assert.Contains("could not confirm that you are a person", error);
            Assert.Contains(HandyFix.Common.GlobalConstants.BusinessPhone, error);
            bookingsService.Verify(
                x => x.CreateBookingAsync(It.IsAny<BookingInputModel>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<string>()),
                Times.Never);
        }

        // Too many photos, one too large, a file that is not a picture: the customer can put that
        // right, so the form comes back saying which.
        [Fact]
        public async Task IndexPostShouldComeBackWithTheReasonWhenAPhotoIsNotOneWeTake()
        {
            var controller = BuildController(out _, out _, out var bookingsService, out var imageService);
            imageService
                .Setup(x => x.UploadImagesAsync(It.IsAny<IEnumerable<IFormFile>>(), It.IsAny<string>()))
                .ThrowsAsync(new ImageUploadValidationException("A maximum of 5 images can be uploaded at once."));

            var result = await controller.Index(ValidModel());

            Assert.IsType<ViewResult>(result);
            Assert.Equal("A maximum of 5 images can be uploaded at once.", SingleModelError(controller));
            bookingsService.Verify(
                x => x.CreateBookingAsync(It.IsAny<BookingInputModel>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<string>()),
                Times.Never);
        }

        // Storage out of reach is not the customer's to fix and not a reason to lose a booking.
        // It used to stop the booking, and a storage that was not set up showed its own error
        // text, settings section and all, to the customer (PROJECT_STATE Section 3cb).
        [Fact]
        public async Task IndexPostShouldTakeTheBookingWithoutItsPhotosWhenStorageCannotBeReached()
        {
            var controller = BuildController(out _, out _, out var bookingsService, out var imageService);
            imageService
                .Setup(x => x.UploadImagesAsync(It.IsAny<IEnumerable<IFormFile>>(), It.IsAny<string>()))
                .ThrowsAsync(new InvalidOperationException("Cloudflare R2 is not fully configured. Missing one or more required settings in CloudflareR2 section."));
            var booking = new Booking();
            bookingsService
                .Setup(x => x.CreateBookingAsync(It.IsAny<BookingInputModel>(), It.Is<IReadOnlyList<string>>(urls => urls.Count == 0), It.IsAny<string>()))
                .ReturnsAsync(booking);

            var result = await controller.Index(ValidModel());

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Pay", redirect.ActionName);
            Assert.Equal(booking.Id, redirect.RouteValues["bookingId"]);
            Assert.Empty(controller.ModelState.SelectMany(entry => entry.Value.Errors));
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

        // A card's "Book Now" names the service. Opened on the default Plumbing tab, a handyman
        // service is not in the list and the page swaps it for the general plumbing one
        // (PROJECT_STATE Section 3br), so the category has to follow the service.
        [Fact]
        public async Task IndexGetShouldTakeTheCategoryFromTheServiceWhenTheLinkGivesNone()
        {
            var controller = BuildController(out var servicesService, out _, out _, out _);
            var handymanService = new ServiceViewModel { Id = Guid.NewGuid(), CategorySlug = "handyman" };
            servicesService
                .Setup(x => x.GetAllAsync<ServiceViewModel>(It.IsAny<bool>()))
                .ReturnsAsync(new List<ServiceViewModel> { new ServiceViewModel { Id = Guid.NewGuid(), CategorySlug = "plumbing" }, handymanService });

            var result = await controller.Index(categorySlug: null, date: null, selectedServiceId: handymanService.Id);

            var model = Assert.IsType<BookingInputModel>(Assert.IsType<ViewResult>(result).Model);
            Assert.Equal("handyman", model.SelectedCategorySlug);
            Assert.Equal(handymanService.Id, model.SelectedServiceId);
        }

        [Fact]
        public async Task IndexGetShouldKeepTheCategoryTheLinkGives()
        {
            var controller = BuildController(out var servicesService, out _, out _, out _);
            var tapRepairs = new ServiceViewModel { Id = Guid.NewGuid(), CategorySlug = "plumbing" };
            servicesService
                .Setup(x => x.GetAllAsync<ServiceViewModel>(It.IsAny<bool>()))
                .ReturnsAsync(new List<ServiceViewModel> { tapRepairs });

            var result = await controller.Index(categorySlug: "handyman", date: null, selectedServiceId: tapRepairs.Id);

            var model = Assert.IsType<BookingInputModel>(Assert.IsType<ViewResult>(result).Model);
            Assert.Equal("handyman", model.SelectedCategorySlug);
        }

        [Fact]
        public async Task IndexGetShouldNotOpenOnACategoryTheWizardCannotBook()
        {
            // Building work is quote-only. The wizard has no tab for it, and its script fails
            // if told to start on one, so a building service must leave the category alone.
            // One controller per call: a controller keeps a single ViewData, so a second call
            // replaces the model the first result points at. With one shared controller this
            // test read the unknown-id answer twice and never checked the building service
            // (PROJECT_STATE Section 3by).
            var kitchenFitting = new ServiceViewModel { Id = Guid.NewGuid(), CategorySlug = "small-building-works" };

            BookingController ControllerOfferingKitchenFitting()
            {
                var controller = BuildController(out var servicesService, out _, out _, out _);
                servicesService
                    .Setup(x => x.GetAllAsync<ServiceViewModel>(It.IsAny<bool>()))
                    .ReturnsAsync(new List<ServiceViewModel> { kitchenFitting });
                return controller;
            }

            var building = await ControllerOfferingKitchenFitting().Index(categorySlug: null, date: null, selectedServiceId: kitchenFitting.Id);
            var unknown = await ControllerOfferingKitchenFitting().Index(categorySlug: null, date: null, selectedServiceId: Guid.NewGuid());

            Assert.Null(Assert.IsType<BookingInputModel>(Assert.IsType<ViewResult>(building).Model).SelectedCategorySlug);
            Assert.Null(Assert.IsType<BookingInputModel>(Assert.IsType<ViewResult>(unknown).Model).SelectedCategorySlug);
        }

        [Fact]
        public async Task IndexGetShouldSendAQuoteOnlyCategoryToTheContactForm()
        {
            // The wizard has no tab for building work, and with no tab ticked its script stops
            // (PROJECT_STATE Section 3bz). The Contact form is where that work is quoted.
            var controller = BuildController(out var servicesService, out _, out _, out _);

            var result = await controller.Index(categorySlug: "Small-Building-Works", date: null, selectedServiceId: null);

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Contact", redirect.ActionName);
            Assert.Equal("Home", redirect.ControllerName);
            Assert.Equal("small-building-works", redirect.RouteValues["categorySlug"]);
            servicesService.Verify(x => x.GetAllAsync<ServiceViewModel>(It.IsAny<bool>()), Times.Never);
        }

        [Fact]
        public async Task IndexGetShouldGiveThePageTheDateFromTheLink()
        {
            // The home page's booking form sends the day the visitor picked. Whether the text in a
            // link is a date at all is the model binder's job, checked in WebTests; here it is
            // already a date or null. One controller per call: a controller keeps a single
            // ViewData, so a second call would replace the model the first result points at.
            var withDate = await BuildController(out _, out _, out _, out _)
                .Index(categorySlug: null, date: new DateTime(2026, 10, 20), selectedServiceId: null);
            var withoutDate = await BuildController(out _, out _, out _, out _)
                .Index(categorySlug: null, date: null, selectedServiceId: null);

            Assert.Equal(new DateTime(2026, 10, 20), Assert.IsType<BookingInputModel>(Assert.IsType<ViewResult>(withDate).Model).SelectedDate);
            Assert.Null(Assert.IsType<BookingInputModel>(Assert.IsType<ViewResult>(withoutDate).Model).SelectedDate);
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
        public async Task ConfirmedShouldReturnTheBookingWhenItsDepositIsPaid()
        {
            var controller = BuildController(out _, out _, out var bookingsService, out _);

            var booking = new BookingDetailsViewModel { Source = BookingSource.Website, StatusName = "Approved", IsDepositPaid = true };
            bookingsService
                .Setup(x => x.GetByIdAsync<BookingDetailsViewModel>(It.IsAny<Guid>()))
                .ReturnsAsync(booking);

            var result = await controller.Confirmed(Guid.NewGuid());

            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Same(booking, viewResult.Model);
        }

        // The page said "Booking Confirmed" and "Deposit Paid" for any booking's id, paid or
        // not (PROJECT_STATE.md Section 3cg). An unpaid one goes to the page that says where it
        // stands; whether it can still be paid for is that page's to work out.
        [Theory]
        [InlineData("Pending", false)]
        [InlineData("Abandoned", false)]
        [InlineData("Cancelled", true)]
        public async Task ConfirmedShouldSendABookingThatIsNotConfirmedToThePaymentPage(string status, bool depositPaid)
        {
            var controller = BuildController(out _, out _, out var bookingsService, out _);

            var id = Guid.NewGuid();
            bookingsService
                .Setup(x => x.GetByIdAsync<BookingDetailsViewModel>(id))
                .ReturnsAsync(new BookingDetailsViewModel { Id = id, Source = BookingSource.Website, StatusName = status, IsDepositPaid = depositPaid });

            var result = await controller.Confirmed(id);

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Cancel", redirect.ActionName);
            Assert.Equal("Payment", redirect.ControllerName);
            Assert.Equal(id, redirect.RouteValues["bookingId"]);
        }

        // A job the admin wrote in is not the customer's to open: it has no deposit and no
        // confirmation page.
        [Fact]
        public async Task ConfirmedShouldReturnNotFoundForAJobTheAdminWroteIn()
        {
            var controller = BuildController(out _, out _, out var bookingsService, out _);

            bookingsService
                .Setup(x => x.GetByIdAsync<BookingDetailsViewModel>(It.IsAny<Guid>()))
                .ReturnsAsync(new BookingDetailsViewModel { Source = BookingSource.Phone, StatusName = "Approved" });

            var result = await controller.Confirmed(Guid.NewGuid());

            Assert.IsType<NotFoundResult>(result);
        }

        private static BookingInputModel ValidModel() => new BookingInputModel
        {
            CustomerFirstName = "Ada",
            CustomerLastName = "Lovelace",
            Email = "ada@example.com",
            PhoneNumber = "07700900123",
            Address = "1 Analytical Engine Way, Chessington",
            Postcode = "KT9 1AA",
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
            out Mock<IImageService> imageService,
            Mock<IServiceAreasService> serviceAreasService = null,
            Mock<IFormGuard> formGuard = null)
        {
            servicesService = new Mock<IServicesService>();
            availabilityService = new Mock<IAvailabilityService>();
            bookingsService = new Mock<IBookingsService>();
            imageService = new Mock<IImageService>();

            // Unless a test says otherwise, every postcode is one we cover.
            if (serviceAreasService == null)
            {
                serviceAreasService = new Mock<IServiceAreasService>();
                serviceAreasService.Setup(x => x.IsPostcodeServedAsync(It.IsAny<string>())).ReturnsAsync(true);
            }

            serviceAreasService
                .Setup(x => x.GetServedPostcodeDistrictsAsync())
                .ReturnsAsync(new List<string> { "KT9", "KT10" });

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
                imageService.Object,
                serviceAreasService.Object,
                (formGuard ?? new Mock<IFormGuard>()).Object,
                NullLogger<BookingController>.Instance);
        }
    }
}
