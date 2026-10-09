namespace HandyFix.Web.Tests.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using HandyFix.Services.Data.Bookings;
    using HandyFix.Services.Data.Technicians;
    using HandyFix.Web.Areas.Administration.Controllers;
    using HandyFix.Web.ViewModels.Administration.Technicians;
    using HandyFix.Web.ViewModels.Booking;

    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.ViewFeatures;

    using Moq;

    using Xunit;

    public class AdminBookingsControllerTests
    {
        /// <summary>
        /// A day guaranteed to fall in the same calendar month as today but not be today,
        /// so the revenue assertion cannot break depending on when the suite is run.
        /// Every month has at least 28 days, so stepping one day away from the 1st (or
        /// back from any other day) always stays inside the month.
        /// </summary>
        private static DateTime AnotherDayThisMonth =>
            DateTime.Today.Day == 1 ? DateTime.Today.AddDays(1) : DateTime.Today.AddDays(-1);

        [Fact]
        public async Task AssignTechnicianShouldPassNullThroughWhenTheBlankOptionIsPosted()
        {
            // Regression for the bug fixed in PROJECT_STATE.md section 3r: the picker's blank
            // "-- Unassigned --" option posts an empty value. While the parameter was a
            // non-nullable Guid that bound to Guid.Empty, which is not a real technician id,
            // so SaveChanges failed the TechnicianId foreign key and the admin got a raw 500.
            var controller = BuildController(out var bookingsService, out _);

            var bookingId = Guid.NewGuid();
            var result = await controller.AssignTechnician(bookingId, null);

            bookingsService.Verify(x => x.AssignTechnicianAsync(bookingId, null), Times.Once);

            // Specifically not Guid.Empty - that is the value that used to blow up.
            bookingsService.Verify(
                x => x.AssignTechnicianAsync(It.IsAny<Guid>(), Guid.Empty),
                Times.Never);

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Details", redirect.ActionName);
            Assert.Equal(bookingId, redirect.RouteValues["id"]);
        }

        [Fact]
        public async Task AssignTechnicianShouldPassTheSelectedTechnicianThrough()
        {
            var controller = BuildController(out var bookingsService, out _);

            var bookingId = Guid.NewGuid();
            var technicianId = Guid.NewGuid();

            await controller.AssignTechnician(bookingId, technicianId);

            bookingsService.Verify(x => x.AssignTechnicianAsync(bookingId, technicianId), Times.Once);
        }

        [Fact]
        public async Task IndexShouldComputeSummaryCardsFromAllBookingsWhenAStatusFilterIsApplied()
        {
            // Regression for PROJECT_STATE.md section 3d: the Today/Waiting/Revenue cards
            // are meant to describe the whole business, not whatever the status filter is
            // currently narrowing the table to. The arithmetic itself now lives in
            // BookingsService.GetSummaryStats (see BookingsServiceTests) - this only checks
            // the controller fetches the *unfiltered* list and hands that to it, not the
            // filtered one.
            var controller = BuildController(out var bookingsService, out _);

            var filtered = new List<BookingDetailsViewModel>
            {
                Booking("Pending", DateTime.Today, 100m),
            };

            var everything = new List<BookingDetailsViewModel>
            {
                Booking("Pending", DateTime.Today, 100m),
                Booking("Approved", DateTime.Today, 250m),
                Booking("Completed", AnotherDayThisMonth, 75m),
            };

            bookingsService
                .Setup(x => x.GetAllBookingsAsync<BookingDetailsViewModel>(It.IsAny<BookingSortField>(), It.IsAny<bool>(), "Pending"))
                .ReturnsAsync(filtered);

            bookingsService
                .Setup(x => x.GetAllBookingsAsync<BookingDetailsViewModel>(BookingSortField.CreatedOn, true, null))
                .ReturnsAsync(everything);

            var expectedSummary = new BookingSummaryStats { TodaysAppointmentsCount = 2, AwaitingTechnicianCount = 1, MonthlyRevenue = 425m };
            bookingsService.Setup(x => x.GetSummaryStats(everything)).Returns(expectedSummary);

            var result = await controller.Index(status: "Pending");

            var model = Assert.IsType<BookingListViewModel>(Assert.IsType<ViewResult>(result).Model);

            // The table shows only the filtered row...
            Assert.Single(model.Bookings);
            Assert.Equal("Pending", model.StatusFilter);

            // ...but the cards come from GetSummaryStats(everything), not GetSummaryStats(filtered).
            bookingsService.Verify(x => x.GetSummaryStats(everything), Times.Once);
            bookingsService.Verify(x => x.GetSummaryStats(filtered), Times.Never);
            Assert.Equal(2, model.TodaysAppointmentsCount);
            Assert.Equal(1, model.AwaitingTechnicianCount);
            Assert.Equal(425m, model.MonthlyRevenue);
        }

        [Fact]
        public async Task IndexShouldNotIssueASecondQueryWhenNoStatusFilterIsApplied()
        {
            // The unfiltered list is already in hand when nothing is filtered, so paying
            // for a duplicate round trip would be waste rather than correctness.
            var controller = BuildController(out var bookingsService, out _);

            bookingsService
                .Setup(x => x.GetAllBookingsAsync<BookingDetailsViewModel>(It.IsAny<BookingSortField>(), It.IsAny<bool>(), It.IsAny<string>()))
                .ReturnsAsync(new List<BookingDetailsViewModel> { Booking("Pending", DateTime.Today, 100m) });

            await controller.Index();

            bookingsService.Verify(
                x => x.GetAllBookingsAsync<BookingDetailsViewModel>(It.IsAny<BookingSortField>(), It.IsAny<bool>(), It.IsAny<string>()),
                Times.Once);
        }

        [Fact]
        public async Task IndexShouldExposeTheStatusOptionsFromTheService()
        {
            var controller = BuildController(out var bookingsService, out _);

            bookingsService
                .Setup(x => x.GetAllBookingsAsync<BookingDetailsViewModel>(It.IsAny<BookingSortField>(), It.IsAny<bool>(), It.IsAny<string>()))
                .ReturnsAsync(new List<BookingDetailsViewModel>());
            bookingsService
                .Setup(x => x.GetStatusOptionsAsync())
                .ReturnsAsync(new[] { "Approved", "Completed", "Pending" });

            var result = await controller.Index();

            var model = Assert.IsType<BookingListViewModel>(Assert.IsType<ViewResult>(result).Model);
            Assert.Equal(new[] { "Approved", "Completed", "Pending" }, model.StatusOptions);
        }

        [Fact]
        public async Task DetailsShouldReturnNotFoundForAnUnknownBooking()
        {
            var controller = BuildController(out var bookingsService, out var techniciansService);

            bookingsService
                .Setup(x => x.GetByIdAsync<BookingDetailsViewModel>(It.IsAny<Guid>()))
                .ReturnsAsync((BookingDetailsViewModel)null);

            var result = await controller.Details(Guid.NewGuid());

            Assert.IsType<NotFoundResult>(result);
            techniciansService.Verify(
                x => x.GetAssignableAsync<TechnicianOptionViewModel>(It.IsAny<Guid?>()),
                Times.Never);
        }

        [Fact]
        public async Task DetailsShouldBuildThePickerAroundTheCurrentlyAssignedTechnician()
        {
            // GetAssignableAsync has to be told who is currently assigned, because a
            // technician who has since been deactivated still needs to appear in this
            // booking's picker - otherwise saving the form would silently unassign them
            // (PROJECT_STATE.md section 3r).
            var controller = BuildController(out var bookingsService, out var techniciansService);

            var assignedId = Guid.NewGuid();
            bookingsService
                .Setup(x => x.GetByIdAsync<BookingDetailsViewModel>(It.IsAny<Guid>()))
                .ReturnsAsync(new BookingDetailsViewModel { TechnicianId = assignedId });

            var options = new List<TechnicianOptionViewModel> { new TechnicianOptionViewModel() };
            techniciansService
                .Setup(x => x.GetAssignableAsync<TechnicianOptionViewModel>(assignedId))
                .ReturnsAsync(options);

            var result = await controller.Details(Guid.NewGuid());

            var model = Assert.IsType<BookingDetailsViewModel>(Assert.IsType<ViewResult>(result).Model);
            Assert.Same(options, model.Technicians);
            techniciansService.Verify(x => x.GetAssignableAsync<TechnicianOptionViewModel>(assignedId), Times.Once);
        }

        // The admin's buttons used to reload the page and say nothing, so "Update Assignment"
        // looked the same whether it had saved or not. Each now leaves a line saying what it did
        // (PROJECT_STATE.md Section 3ce). The line is what tells the admin whether the customer
        // was emailed, so its wording is checked for the part that matters.
        [Theory]
        [InlineData(TechnicianAssignmentOutcome.AssignedAndCustomerEmailed, "Zapryan", "SuccessMessage", "Zapryan is now the technician for this booking. The customer has been emailed")]
        [InlineData(TechnicianAssignmentOutcome.AssignedButEmailNotSent, "Zapryan", "ErrorMessage", "the email to the customer could not be sent. Please give them the name and phone number yourself")]
        [InlineData(TechnicianAssignmentOutcome.Cleared, null, "SuccessMessage", "This booking has no technician now. The customer has not been emailed")]
        [InlineData(TechnicianAssignmentOutcome.Unchanged, "Zapryan", "SuccessMessage", "Nothing was changed: Zapryan was already the technician. No email was sent")]
        [InlineData(TechnicianAssignmentOutcome.Unchanged, null, "SuccessMessage", "Nothing was changed: this booking had no technician")]
        [InlineData(TechnicianAssignmentOutcome.TechnicianNotFound, null, "ErrorMessage", "Nothing was changed. That technician is no longer on the roster")]
        [InlineData(TechnicianAssignmentOutcome.NotAllowed, null, "ErrorMessage", "Nothing was changed. A technician can be picked once the deposit is paid")]
        public async Task AssignTechnicianShouldSayWhatHappened(TechnicianAssignmentOutcome outcome, string technicianName, string messageKey, string expectedText)
        {
            var controller = BuildController(out var bookingsService, out _);
            var bookingId = Guid.NewGuid();
            bookingsService
                .Setup(x => x.AssignTechnicianAsync(bookingId, It.IsAny<Guid?>()))
                .ReturnsAsync(new TechnicianAssignmentResult { Outcome = outcome, TechnicianName = technicianName });

            var result = await controller.AssignTechnician(bookingId, Guid.NewGuid());

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Details", redirect.ActionName);
            Assert.Equal(bookingId, redirect.RouteValues["id"]);
            Assert.Contains(expectedText, Assert.IsType<string>(controller.TempData[messageKey]));
            Assert.Null(controller.TempData[messageKey == "SuccessMessage" ? "ErrorMessage" : "SuccessMessage"]);
        }

        [Fact]
        public async Task AssignTechnicianShouldReturnNotFoundForAnUnknownBooking()
        {
            var controller = BuildController(out var bookingsService, out _);
            bookingsService
                .Setup(x => x.AssignTechnicianAsync(It.IsAny<Guid>(), It.IsAny<Guid?>()))
                .ReturnsAsync(new TechnicianAssignmentResult { Outcome = TechnicianAssignmentOutcome.BookingNotFound });

            var result = await controller.AssignTechnician(Guid.NewGuid(), Guid.NewGuid());

            Assert.IsType<NotFoundResult>(result);
        }

        [Theory]
        [InlineData(true, "SuccessMessage", "The booking is marked as completed.")]
        [InlineData(false, "ErrorMessage", "Nothing was changed. A booking can be marked as completed once its deposit is paid")]
        public async Task CompleteShouldSayWhetherTheBookingWasCompletedAndReturnToDetails(bool completed, string messageKey, string expectedText)
        {
            var controller = BuildController(out var bookingsService, out _);
            var bookingId = Guid.NewGuid();
            bookingsService.Setup(x => x.CompleteBookingAsync(bookingId)).ReturnsAsync(completed);

            var result = await controller.Complete(bookingId);

            bookingsService.Verify(x => x.CompleteBookingAsync(bookingId), Times.Once);

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Details", redirect.ActionName);
            Assert.Equal(bookingId, redirect.RouteValues["id"]);
            Assert.Contains(expectedText, Assert.IsType<string>(controller.TempData[messageKey]));
        }

        // The site sends no email when a booking is cancelled, and refunds are made by hand. The
        // line the admin is left with has to say both, or nobody tells the customer.
        [Theory]
        [InlineData(true, "SuccessMessage", "The booking is cancelled and its time slot can be booked again. The customer has not been emailed")]
        [InlineData(false, "ErrorMessage", "Nothing was changed. This booking is already completed, cancelled or abandoned.")]
        public async Task CancelShouldSayWhetherTheBookingWasCancelledAndReturnToDetails(bool cancelled, string messageKey, string expectedText)
        {
            var controller = BuildController(out var bookingsService, out _);
            var bookingId = Guid.NewGuid();
            bookingsService.Setup(x => x.CancelBookingAsync(bookingId)).ReturnsAsync(cancelled);

            var result = await controller.Cancel(bookingId);

            // Cancelling has to release the slot too, which is CancelBookingAsync's job.
            bookingsService.Verify(x => x.CancelBookingAsync(bookingId), Times.Once);
            bookingsService.Verify(x => x.CompleteBookingAsync(It.IsAny<Guid>()), Times.Never);

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Details", redirect.ActionName);
            Assert.Equal(bookingId, redirect.RouteValues["id"]);
            Assert.Contains(expectedText, Assert.IsType<string>(controller.TempData[messageKey]));
        }

        // A booking is approved by its deposit being paid. The button that did it by hand showed
        // only on an unpaid booking, and the email that hung off it could never be sent for a
        // paid one.
        [Fact]
        public void ThereIsNoApproveAction()
        {
            Assert.Null(typeof(BookingsController).GetMethod("Approve"));
        }

        private static BookingDetailsViewModel Booking(string status, DateTime scheduledTime, decimal total) =>
            new BookingDetailsViewModel
            {
                Id = Guid.NewGuid(),
                StatusName = status,
                ScheduledTime = scheduledTime,
                TotalAmount = total,
            };

        private static BookingsController BuildController(
            out Mock<IBookingsService> bookingsService,
            out Mock<ITechniciansService> techniciansService)
        {
            bookingsService = new Mock<IBookingsService>();
            techniciansService = new Mock<ITechniciansService>();

            // Safe non-null defaults so tests that don't care about the summary cards or
            // status dropdown don't have to set them up individually.
            bookingsService
                .Setup(x => x.GetSummaryStats(It.IsAny<IEnumerable<BookingDetailsViewModel>>()))
                .Returns(new BookingSummaryStats());
            bookingsService
                .Setup(x => x.GetStatusOptionsAsync())
                .ReturnsAsync(Array.Empty<string>());
            bookingsService
                .Setup(x => x.AssignTechnicianAsync(It.IsAny<Guid>(), It.IsAny<Guid?>()))
                .ReturnsAsync(new TechnicianAssignmentResult { Outcome = TechnicianAssignmentOutcome.Cleared });

            // The actions leave their message in TempData, which a controller built by hand
            // does not have until it is given one.
            return new BookingsController(
                bookingsService.Object,
                techniciansService.Object)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
                TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>()),
            };
        }
    }
}
