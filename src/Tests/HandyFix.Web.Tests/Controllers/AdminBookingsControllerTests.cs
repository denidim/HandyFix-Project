namespace HandyFix.Web.Tests.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using HandyFix.Data.Models;
    using HandyFix.Services.Data.Bookings;
    using HandyFix.Services.Data.Inquiries;
    using HandyFix.Services.Data.Payments;
    using HandyFix.Services.Data.Services;
    using HandyFix.Services.Data.Technicians;
    using HandyFix.Web.Areas.Administration.Controllers;
    using HandyFix.Web.ViewModels.Administration.Enquiries;
    using HandyFix.Web.ViewModels.Administration.Technicians;
    using HandyFix.Web.ViewModels.Booking;
    using HandyFix.Web.ViewModels.Services;

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
        [InlineData(TechnicianAssignmentOutcome.AssignedAndCustomerEmailed, "Zapryan", "SuccessMessage", "Zapryan is now the technician for this job. The customer has been emailed")]
        [InlineData(TechnicianAssignmentOutcome.AssignedButEmailNotSent, "Zapryan", "ErrorMessage", "the email to the customer could not be sent. Please give them the name and phone number yourself")]
        [InlineData(TechnicianAssignmentOutcome.AssignedNoEmailForWrittenInJob, "Zapryan", "SuccessMessage", "Zapryan is now the technician for this job. No email goes out for a job that was written in: please tell the customer yourself")]
        [InlineData(TechnicianAssignmentOutcome.Cleared, null, "SuccessMessage", "This job has no technician now. The customer has not been emailed")]
        [InlineData(TechnicianAssignmentOutcome.Unchanged, "Zapryan", "SuccessMessage", "Nothing was changed: Zapryan was already the technician. No email was sent")]
        [InlineData(TechnicianAssignmentOutcome.Unchanged, null, "SuccessMessage", "Nothing was changed: this job had no technician")]
        [InlineData(TechnicianAssignmentOutcome.TechnicianNotFound, null, "ErrorMessage", "Nothing was changed. That technician is no longer on the roster")]
        [InlineData(TechnicianAssignmentOutcome.NotAllowed, null, "ErrorMessage", "Nothing was changed. A technician can be picked while the job is booked, and on a website booking only once its deposit is paid")]
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
        [InlineData(true, "SuccessMessage", "The job is marked as done, at a final price of £135.00.")]
        [InlineData(false, "ErrorMessage", "Nothing was changed. A job can be marked as done while it is booked, and a website booking only once its deposit is paid.")]
        public async Task CompleteShouldPassTheFinalPriceOnAndSayWhetherTheJobWasMarkedDone(bool completed, string messageKey, string expectedText)
        {
            var controller = BuildController(out var bookingsService, out _);
            var bookingId = Guid.NewGuid();
            bookingsService.Setup(x => x.CompleteBookingAsync(bookingId, 135.00m)).ReturnsAsync(completed);

            var result = await controller.Complete(bookingId, 135.00m);

            bookingsService.Verify(x => x.CompleteBookingAsync(bookingId, 135.00m), Times.Once);

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Details", redirect.ActionName);
            Assert.Equal(bookingId, redirect.RouteValues["id"]);
            Assert.Equal(expectedText, Assert.IsType<string>(controller.TempData[messageKey]));
        }

        // A job is finished with what it came to. Sent with no price, or with nothing in the
        // box, it is not marked done and the service is not asked.
        [Theory]
        [InlineData(null)]
        [InlineData(0.0)]
        [InlineData(-20.0)]
        public async Task CompleteShouldAskForTheFinalPriceWhenThereIsNone(double? finalPrice)
        {
            var controller = BuildController(out var bookingsService, out _);
            var bookingId = Guid.NewGuid();

            await controller.Complete(bookingId, (decimal?)finalPrice);

            bookingsService.Verify(x => x.CompleteBookingAsync(It.IsAny<Guid>(), It.IsAny<decimal>()), Times.Never);
            Assert.Contains("Type the final price, more than £0", Assert.IsType<string>(controller.TempData["ErrorMessage"]));
        }

        [Theory]
        [InlineData(true, "SuccessMessage", "The final price is now £140.00.")]
        [InlineData(false, "ErrorMessage", "Nothing was changed. The final price can be put right on a job that is done")]
        public async Task FinalPriceShouldSayWhetherItWasChanged(bool changed, string messageKey, string expectedText)
        {
            var controller = BuildController(out var bookingsService, out _);
            var bookingId = Guid.NewGuid();
            bookingsService.Setup(x => x.ChangeFinalPriceAsync(bookingId, 140.00m)).ReturnsAsync(changed);

            await controller.FinalPrice(bookingId, 140.00m);

            Assert.Contains(expectedText, Assert.IsType<string>(controller.TempData[messageKey]));
        }

        // The site sends no email when a job is cancelled, and refunds are made by hand. The
        // line the admin is left with has to say both, or nobody tells the customer.
        [Theory]
        [InlineData(true, "SuccessMessage", "The job is cancelled. It keeps its date and the reason, and an hour it held in the calendar can be booked again. The customer has not been emailed: please tell them yourself.")]
        [InlineData(false, "ErrorMessage", "Nothing was changed. This job is already done, cancelled or abandoned.")]
        public async Task CancelShouldPassTheReasonOnAndSayWhetherTheJobWasCancelled(bool cancelled, string messageKey, string expectedText)
        {
            var controller = BuildController(out var bookingsService, out _);
            var bookingId = Guid.NewGuid();
            bookingsService.Setup(x => x.CancelBookingAsync(bookingId, "The customer moved away.")).ReturnsAsync(cancelled);

            var result = await controller.Cancel(bookingId, "The customer moved away.");

            // Cancelling has to release the slot too, which is CancelBookingAsync's job.
            bookingsService.Verify(x => x.CancelBookingAsync(bookingId, "The customer moved away."), Times.Once);
            bookingsService.Verify(x => x.CompleteBookingAsync(It.IsAny<Guid>(), It.IsAny<decimal>()), Times.Never);

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Details", redirect.ActionName);
            Assert.Equal(bookingId, redirect.RouteValues["id"]);
            Assert.Contains(expectedText, Assert.IsType<string>(controller.TempData[messageKey]));
            if (cancelled)
            {
                Assert.Contains("refunded by hand in Stripe", Assert.IsType<string>(controller.TempData[messageKey]));
            }
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task CancelShouldAskForTheReasonWhenThereIsNone(string reason)
        {
            var controller = BuildController(out var bookingsService, out _);

            await controller.Cancel(Guid.NewGuid(), reason);

            bookingsService.Verify(x => x.CancelBookingAsync(It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);
            Assert.Contains("Write the reason for cancelling", Assert.IsType<string>(controller.TempData["ErrorMessage"]));
        }

        // A website booking carries its hour in the calendar with it and a written-in job holds
        // none, so what a move did to the calendar differs. The line says which, and always
        // ends by saying the customer was not emailed.
        [Theory]
        [InlineData(true, true, false, "The hour it had is back on sale. The new hour is taken off sale.")]
        [InlineData(true, false, true, "The hour it had is back on sale. The new hour is not free in the calendar, so nothing was taken off sale")]
        [InlineData(false, false, false, "Nothing was changed in the calendar: block the new hour by hand")]
        public async Task MoveShouldSayWhereTheJobWentAndWhatHappenedInTheCalendar(bool oldHourFreed, bool newHourTaken, bool newHourNotAvailable, string expectedText)
        {
            var controller = BuildController(out var bookingsService, out _);
            var bookingId = Guid.NewGuid();
            var newStart = new DateTime(2026, 10, 14, 9, 0, 0);
            bookingsService
                .Setup(x => x.MoveBookingAsync(bookingId, newStart))
                .ReturnsAsync(new JobMoveResult
                {
                    Outcome = JobMoveOutcome.Moved,
                    NewStart = newStart,
                    OldHourFreed = oldHourFreed,
                    NewHourTaken = newHourTaken,
                    NewHourNotAvailable = newHourNotAvailable,
                });

            var result = await controller.Move(bookingId, new DateTime(2026, 10, 14), new TimeSpan(9, 0, 0));

            var message = Assert.IsType<string>(controller.TempData["SuccessMessage"]);
            Assert.StartsWith("The job is moved to Wednesday 14 October 2026 at 09:00.", message);
            Assert.Contains(expectedText, message);
            Assert.EndsWith("The customer has not been emailed: please tell them yourself.", message);
            Assert.Equal("Details", Assert.IsType<RedirectToActionResult>(result).ActionName);
        }

        [Theory]
        [InlineData(JobMoveOutcome.Unchanged, "SuccessMessage", "Nothing was changed: the job is already at that day and time.")]
        [InlineData(JobMoveOutcome.CalendarChanged, "ErrorMessage", "Nothing was changed: the calendar changed in the same moment. Please try again.")]
        [InlineData(JobMoveOutcome.NotAllowed, "ErrorMessage", "Nothing was changed. A job can be moved while it is booked")]
        public async Task MoveShouldSayWhyNothingMoved(JobMoveOutcome outcome, string messageKey, string expectedText)
        {
            var controller = BuildController(out var bookingsService, out _);
            bookingsService
                .Setup(x => x.MoveBookingAsync(It.IsAny<Guid>(), It.IsAny<DateTime>()))
                .ReturnsAsync(new JobMoveResult { Outcome = outcome });

            await controller.Move(Guid.NewGuid(), new DateTime(2026, 10, 14), new TimeSpan(9, 0, 0));

            Assert.Contains(expectedText, Assert.IsType<string>(controller.TempData[messageKey]));
        }

        [Fact]
        public async Task MoveShouldAskForBothTheDayAndTheTime()
        {
            var controller = BuildController(out var bookingsService, out _);

            await controller.Move(Guid.NewGuid(), new DateTime(2026, 10, 14), null);

            bookingsService.Verify(x => x.MoveBookingAsync(It.IsAny<Guid>(), It.IsAny<DateTime>()), Times.Never);
            Assert.Contains("Pick both the new day and the new time", Assert.IsType<string>(controller.TempData["ErrorMessage"]));
        }

        [Fact]
        public async Task MoveShouldReturnNotFoundForAnUnknownJob()
        {
            var controller = BuildController(out var bookingsService, out _);
            bookingsService
                .Setup(x => x.MoveBookingAsync(It.IsAny<Guid>(), It.IsAny<DateTime>()))
                .ReturnsAsync(new JobMoveResult { Outcome = JobMoveOutcome.BookingNotFound });

            Assert.IsType<NotFoundResult>(await controller.Move(Guid.NewGuid(), new DateTime(2026, 10, 14), new TimeSpan(9, 0, 0)));
        }

        [Theory]
        [InlineData("Card", "£85.00 is written on the job, paid by card.")]
        [InlineData("Bank transfer", "£85.00 is written on the job, paid by bank transfer.")]
        public async Task AddPaymentShouldSayWhatWasWrittenOnTheJob(string method, string expectedText)
        {
            var controller = BuildController(out _, out _, out var paymentsService);
            var bookingId = Guid.NewGuid();
            paymentsService.Setup(x => x.AddPaymentAsync(bookingId, 85.00m, method)).ReturnsAsync(true);

            await controller.AddPayment(bookingId, 85.00m, method);

            Assert.Equal(expectedText, Assert.IsType<string>(controller.TempData["SuccessMessage"]));
        }

        [Theory]
        [InlineData(null, false)]
        [InlineData(0.0, false)]
        [InlineData(85.0, true)]
        public async Task AddPaymentShouldSayWhenNothingWasWritten(double? amount, bool serviceIsAsked)
        {
            var controller = BuildController(out _, out _, out var paymentsService);
            paymentsService.Setup(x => x.AddPaymentAsync(It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<string>())).ReturnsAsync(false);

            await controller.AddPayment(Guid.NewGuid(), (decimal?)amount, "Cash");

            paymentsService.Verify(
                x => x.AddPaymentAsync(It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<string>()),
                serviceIsAsked ? Times.Once() : Times.Never());
            Assert.Contains("Nothing was changed. A payment can be written on a job that is booked or done", Assert.IsType<string>(controller.TempData["ErrorMessage"]));
        }

        [Theory]
        [InlineData(true, "SuccessMessage", "The payment is taken off the job.")]
        [InlineData(false, "ErrorMessage", "Nothing was changed. Only a payment that was written on the job by hand can be taken off.")]
        public async Task RemovePaymentShouldSayWhetherItWasTakenOff(bool removed, string messageKey, string expectedText)
        {
            var controller = BuildController(out _, out _, out var paymentsService);
            var bookingId = Guid.NewGuid();
            var paymentId = Guid.NewGuid();
            paymentsService.Setup(x => x.RemovePaymentAsync(bookingId, paymentId)).ReturnsAsync(removed);

            await controller.RemovePayment(bookingId, paymentId);

            Assert.Equal(expectedText, Assert.IsType<string>(controller.TempData[messageKey]));
        }

        [Theory]
        [InlineData(true, true, "SuccessMessage", "The deposit is marked as refunded. It no longer counts as money in.")]
        [InlineData(false, true, "SuccessMessage", "The deposit is marked as not refunded.")]
        [InlineData(true, false, "ErrorMessage", "Nothing was changed. Only the deposit of a cancelled job can be marked as refunded.")]
        public async Task DepositRefundedShouldSayWhatTheTickDid(bool refunded, bool saved, string messageKey, string expectedText)
        {
            var controller = BuildController(out _, out _, out var paymentsService);
            var bookingId = Guid.NewGuid();
            paymentsService.Setup(x => x.SetDepositRefundedAsync(bookingId, refunded)).ReturnsAsync(saved);

            await controller.DepositRefunded(bookingId, refunded);

            Assert.Contains(expectedText, Assert.IsType<string>(controller.TempData[messageKey]));
        }

        [Fact]
        public async Task NotesShouldSaveAndSayOnlyTheAdminSeesThem()
        {
            var controller = BuildController(out var bookingsService, out _);
            var bookingId = Guid.NewGuid();
            bookingsService.Setup(x => x.SaveNotesAsync(bookingId, "Stopcock under the sink.")).ReturnsAsync(true);

            var result = await controller.Notes(bookingId, "Stopcock under the sink.");

            Assert.Equal("Details", Assert.IsType<RedirectToActionResult>(result).ActionName);
            Assert.Equal("The notes are saved. Only the admin sees them.", controller.TempData["SuccessMessage"]);

            Assert.IsType<NotFoundResult>(await controller.Notes(Guid.NewGuid(), "Nobody's."));
        }

        // "Make this a job" on an enquiry opens the form with what the enquiry holds. An enquiry
        // has one name box: the first word is taken as the first name and the rest as the last.
        [Theory]
        [InlineData("Jane Mary Doe", "Jane", "Mary Doe")]
        [InlineData("Cher", "Cher", null)]
        public async Task CreateShouldStartFromTheEnquiryItWasOpenedFrom(string enquiryName, string firstName, string lastName)
        {
            var controller = BuildController(out _, out _, out _, out var inquiriesService);
            var enquiryId = Guid.NewGuid();
            inquiriesService
                .Setup(x => x.GetByIdAsync<EnquiryViewModel>(enquiryId))
                .ReturnsAsync(new EnquiryViewModel
                {
                    Id = enquiryId,
                    Name = enquiryName,
                    PhoneNumber = "07700 900123",
                    Email = "jane.doe@example.com",
                    Message = "[Category: Plumbing] The stopcock will not turn.",
                });

            var result = await controller.Create(enquiryId);

            var view = Assert.IsType<ViewResult>(result);
            Assert.Equal("Create", view.ViewName);
            var model = Assert.IsType<JobInputModel>(view.Model);
            Assert.Equal(firstName, model.CustomerFirstName);
            Assert.Equal(lastName, model.CustomerLastName);
            Assert.Equal("07700 900123", model.PhoneNumber);
            Assert.Equal("jane.doe@example.com", model.Email);
            Assert.Equal("[Category: Plumbing] The stopcock will not turn.", model.ProblemDescription);
            Assert.Equal(BookingSource.Enquiry, model.Source);
            Assert.Equal(enquiryId, model.EnquiryId);
        }

        // Opened from a day in the calendar, the form starts with that day; opened from the
        // list, with nothing but "Phone", the commonest way a job is written in.
        [Fact]
        public async Task CreateShouldStartWithTheDayItWasOpenedFor()
        {
            var controller = BuildController(out _, out _);

            var result = await controller.Create(date: new DateTime(2026, 10, 14, 13, 30, 0));

            var model = Assert.IsType<JobInputModel>(Assert.IsType<ViewResult>(result).Model);
            Assert.Equal(new DateTime(2026, 10, 14), model.Date);
            Assert.Null(model.Time);
            Assert.Equal(BookingSource.Phone, model.Source);
            Assert.Null(model.EnquiryId);
        }

        [Fact]
        public async Task CreateShouldReturnNotFoundForAnEnquiryThatIsGone()
        {
            var controller = BuildController(out _, out _);

            Assert.IsType<NotFoundResult>(await controller.Create(Guid.NewGuid()));
        }

        [Fact]
        public async Task CreateShouldWriteTheJobInAndOpenItsPage()
        {
            var controller = BuildController(out var bookingsService, out _);
            var newId = Guid.NewGuid();
            var form = new JobInputModel { CustomerFirstName = "Grace", PhoneNumber = "07700 900456", Date = new DateTime(2026, 10, 14), Time = new TimeSpan(14, 0, 0), Source = BookingSource.Phone };
            bookingsService.Setup(x => x.CreateWrittenInJobAsync(form)).ReturnsAsync(newId);

            var result = await controller.Create(form);

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Details", redirect.ActionName);
            Assert.Equal(newId, redirect.RouteValues["id"]);

            // The two things the admin has to know: the hour is still on sale, and nobody was emailed.
            var message = Assert.IsType<string>(controller.TempData["SuccessMessage"]);
            Assert.Contains("Its hour is still on sale on the website", message);
            Assert.Contains("No email was sent to the customer", message);
        }

        // The website is not on the form's list. A form sent without the page could still name
        // it, and a "website" job with no deposit would look like a paid booking in the list.
        [Fact]
        public async Task CreateShouldRefuseAJobThatClaimsToComeFromTheWebsite()
        {
            var controller = BuildController(out var bookingsService, out _);
            var form = new JobInputModel { CustomerFirstName = "Grace", PhoneNumber = "07700 900456", Date = new DateTime(2026, 10, 14), Time = new TimeSpan(14, 0, 0), Source = BookingSource.Website };

            var result = await controller.Create(form);

            Assert.Equal("Create", Assert.IsType<ViewResult>(result).ViewName);
            Assert.False(controller.ModelState.IsValid);
            bookingsService.Verify(x => x.CreateWrittenInJobAsync(It.IsAny<JobInputModel>()), Times.Never);
        }

        // The job's page shows its money list and its history, fetched for that page only: the
        // list of jobs does not carry them for every row.
        [Fact]
        public async Task DetailsShouldCarryTheMoneyListAndTheHistory()
        {
            var controller = BuildController(out var bookingsService, out _, out var paymentsService);
            var bookingId = Guid.NewGuid();
            var payments = new List<PaymentLineViewModel> { new PaymentLineViewModel { Amount = 50m, StatusName = "DepositPaid" } };
            var history = new List<BookingHistoryViewModel> { new BookingHistoryViewModel { Text = "Booked on the website." } };
            bookingsService.Setup(x => x.GetByIdAsync<BookingDetailsViewModel>(bookingId)).ReturnsAsync(new BookingDetailsViewModel { Id = bookingId });
            bookingsService.Setup(x => x.GetHistoryAsync<BookingHistoryViewModel>(bookingId)).ReturnsAsync(history);
            paymentsService.Setup(x => x.GetMoneyListAsync<PaymentLineViewModel>(bookingId)).ReturnsAsync(payments);

            var result = await controller.Details(bookingId);

            var model = Assert.IsType<BookingDetailsViewModel>(Assert.IsType<ViewResult>(result).Model);
            Assert.Same(payments, model.Payments);
            Assert.Same(history, model.History);
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
            return BuildController(out bookingsService, out techniciansService, out _, out _);
        }

        private static BookingsController BuildController(
            out Mock<IBookingsService> bookingsService,
            out Mock<ITechniciansService> techniciansService,
            out Mock<IPaymentsService> paymentsService)
        {
            return BuildController(out bookingsService, out techniciansService, out paymentsService, out _);
        }

        private static BookingsController BuildController(
            out Mock<IBookingsService> bookingsService,
            out Mock<ITechniciansService> techniciansService,
            out Mock<IPaymentsService> paymentsService,
            out Mock<IInquiriesService> inquiriesService)
        {
            bookingsService = new Mock<IBookingsService>();
            techniciansService = new Mock<ITechniciansService>();
            paymentsService = new Mock<IPaymentsService>();
            inquiriesService = new Mock<IInquiriesService>();

            var servicesService = new Mock<IServicesService>();
            servicesService
                .Setup(x => x.GetAllAsync<ServiceViewModel>(It.IsAny<bool>()))
                .ReturnsAsync(new List<ServiceViewModel>());

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
                techniciansService.Object,
                paymentsService.Object,
                servicesService.Object,
                inquiriesService.Object)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
                TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>()),
            };
        }
    }
}
