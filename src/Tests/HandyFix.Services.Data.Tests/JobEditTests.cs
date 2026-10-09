namespace HandyFix.Services.Data.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Common;
    using HandyFix.Data.Models;
    using HandyFix.Services.Data.Common;
    using HandyFix.Web.ViewModels.Booking;

    using Microsoft.EntityFrameworkCore;

    using Xunit;

    // Putting a job's details right (PROJECT_STATE.md Section 3cf). The Job Book left it out:
    // a phone number typed wrongly meant cancelling the job and writing it in again. These are
    // the rules behind the form: which jobs it works on, what a website booking keeps, what a
    // changed service does to the estimate, and the line each save leaves in the history.
    public class JobEditTests
    {
        [Fact]
        public async Task TheDetailsOfAWrittenInJobCanBePutRight()
        {
            using var world = new BookingWorld();
            Booking job = await world.SeedWrittenInJobAsync();
            DateTime? start = job.ScheduledStart;
            JobEditInputModel form = world.EditFormOf(job);
            form.CustomerLastName = "  Hopper ";
            form.PhoneNumber = " 07700 900999 ";
            form.Address = "3 Navy Row, Epsom";

            JobEditResult result = await world.Bookings.EditDetailsAsync(form);

            Assert.Equal(JobEditOutcome.Saved, result.Outcome);
            Assert.Equal(new[] { "last name", "phone number", "address" }, result.Changed);
            Assert.False(result.EmailChangedOnWebsiteBooking);

            Booking after = world.DbContext.Bookings.Single();
            Assert.Equal("Grace", after.CustomerFirstName);
            Assert.Equal("Hopper", after.CustomerLastName);
            Assert.Equal("07700 900999", after.PhoneNumber);
            Assert.Equal("3 Navy Row, Epsom", after.Address);

            // The line keeps what each box held before: the page shows what it holds now.
            Assert.Equal(
                "Details changed. Last name was empty. Phone number was 07700 900456. Address was not written down.",
                world.HistoryOf(job).Last());

            // The form has no day or time on it, and leaves both where they were.
            Assert.Equal(start, after.ScheduledStart);
            Assert.Equal("Approved", world.StatusOf(job));
        }

        // The form opens filled in. Saved untouched, or with a box emptied that was already
        // empty, it is the same job and leaves no line.
        [Fact]
        public async Task SavingTheDetailsAsTheyStandChangesNothingAndLeavesNoLine()
        {
            using var world = new BookingWorld();
            Booking job = await world.SeedWrittenInJobAsync();
            var lines = world.HistoryOf(job).Count;
            JobEditInputModel form = world.EditFormOf(job);
            form.Address = "   ";
            form.CustomerLastName = string.Empty;

            JobEditResult result = await world.Bookings.EditDetailsAsync(form);

            Assert.Equal(JobEditOutcome.Unchanged, result.Outcome);
            Assert.Empty(result.Changed);
            Assert.Equal(lines, world.HistoryOf(job).Count);
            Assert.Null(world.DbContext.Bookings.Single().Address);
        }

        [Theory]
        [InlineData("Pending", true)]
        [InlineData("Approved", true)]
        [InlineData("InProgress", true)]
        [InlineData("Completed", true)]
        [InlineData("Cancelled", false)]
        [InlineData("Abandoned", false)]
        public void AJobsDetailsCanBePutRightWhileItIsBookedOrDone(string statusName, bool allowed)
        {
            Assert.Equal(allowed, BookingRules.CanEditDetails(statusName));
        }

        // A cancelled or abandoned job is a record of what happened. The page has no button for
        // it; a form that was opened before the job fell away is refused here.
        [Theory]
        [InlineData("Cancelled")]
        [InlineData("Abandoned")]
        public async Task AJobThatFellAwayKeepsTheDetailsItEndedWith(string status)
        {
            using var world = new BookingWorld();
            Booking booking = await world.SeedPaidBookingAsync();
            JobEditInputModel form = world.EditFormOf(booking);
            form.PhoneNumber = "07700 900999";
            world.SetStatus(booking, status);
            var lines = world.HistoryOf(booking).Count;

            JobEditResult result = await world.Bookings.EditDetailsAsync(form);

            Assert.Equal(JobEditOutcome.NotAllowed, result.Outcome);
            Assert.Equal("07700 900123", world.DbContext.Bookings.Single().PhoneNumber);
            Assert.Equal(lines, world.HistoryOf(booking).Count);
        }

        // The address matters after the job is done too: it is what an invoice will carry.
        [Fact]
        public async Task ADoneJobsDetailsCanStillBePutRight()
        {
            using var world = new BookingWorld();
            Booking booking = await world.SeedPaidBookingAsync();
            await world.Bookings.CompleteBookingAsync(booking.Id, 135.00m);
            JobEditInputModel form = world.EditFormOf(booking);
            form.Address = "14 Main Rd, Sutton";

            JobEditResult result = await world.Bookings.EditDetailsAsync(form);

            Assert.Equal(JobEditOutcome.Saved, result.Outcome);
            Assert.Equal("14 Main Rd, Sutton", world.DbContext.Bookings.Single().Address);
            Assert.Equal("Details changed. Address was 12 Main Rd, Sutton.", world.HistoryOf(booking).Last());
            Assert.Equal("Completed", world.StatusOf(booking));
        }

        // The page of a website booking has no "Came from" box. A form sent without the page
        // can still name one, and the booking stays what the site made it.
        [Fact]
        public async Task AWebsiteBookingStaysAWebsiteBookingWhateverTheFormSends()
        {
            using var world = new BookingWorld();
            Booking booking = await world.SeedPaidBookingAsync();
            JobEditInputModel form = world.EditFormOf(booking);
            form.Source = BookingSource.Phone;
            form.PhoneNumber = "07700 900999";

            JobEditResult result = await world.Bookings.EditDetailsAsync(form);

            Assert.Equal(JobEditOutcome.Saved, result.Outcome);
            Assert.Equal(new[] { "phone number" }, result.Changed);
            Assert.Equal(BookingSource.Website, world.DbContext.Bookings.Single().Source);
        }

        // The site emails a website customer when a technician is picked. With no address on
        // the job that email has nowhere to go, so the job keeps one.
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task AWebsiteBookingKeepsAnEmail(string email)
        {
            using var world = new BookingWorld();
            Booking booking = await world.SeedPaidBookingAsync();
            var lines = world.HistoryOf(booking).Count;
            JobEditInputModel form = world.EditFormOf(booking);
            form.Email = email;
            form.PhoneNumber = "07700 900999";

            JobEditResult result = await world.Bookings.EditDetailsAsync(form);

            Assert.Equal(JobEditOutcome.EmailNeeded, result.Outcome);
            Booking after = world.DbContext.Bookings.Single();
            Assert.Equal("ada@example.com", after.Email);
            Assert.Equal("07700 900123", after.PhoneNumber);
            Assert.Equal(lines, world.HistoryOf(booking).Count);
        }

        // A written-in job's email is optional when it is made and stays so.
        [Fact]
        public async Task AWrittenInJobsEmailCanBeTakenOff()
        {
            using var world = new BookingWorld();
            JobInputModel written = world.WrittenInJob();
            written.Email = "grace@example.com";
            Guid id = await world.Bookings.CreateWrittenInJobAsync(written);
            Booking job = world.DbContext.Bookings.Single(x => x.Id == id);
            JobEditInputModel form = world.EditFormOf(job);
            form.Email = null;

            JobEditResult result = await world.Bookings.EditDetailsAsync(form);

            Assert.Equal(JobEditOutcome.Saved, result.Outcome);
            Assert.False(result.EmailChangedOnWebsiteBooking);
            Assert.Null(world.DbContext.Bookings.Single().Email);
            Assert.Equal("Details changed. Email was grace@example.com.", world.HistoryOf(job).Last());
        }

        [Fact]
        public async Task WhereAWrittenInJobCameFromCanBePutRight()
        {
            using var world = new BookingWorld();
            Booking job = await world.SeedWrittenInJobAsync();
            JobEditInputModel form = world.EditFormOf(job);
            form.Source = BookingSource.Agency;

            JobEditResult result = await world.Bookings.EditDetailsAsync(form);

            Assert.Equal(JobEditOutcome.Saved, result.Outcome);
            Assert.Equal(new[] { "where it came from" }, result.Changed);
            Assert.Equal(BookingSource.Agency, world.DbContext.Bookings.Single().Source);
            Assert.Equal("Details changed. Where it came from was Phone.", world.HistoryOf(job).Last());
        }

        // "Website" is the one place a job cannot be said to come from by hand: with no deposit
        // and no slot it would sit in the list looking like a paid booking's twin.
        [Theory]
        [InlineData(null)]
        [InlineData(BookingSource.Website)]
        public async Task AWrittenInJobCannotBeMadeAWebsiteBooking(BookingSource? source)
        {
            using var world = new BookingWorld();
            Booking job = await world.SeedWrittenInJobAsync();
            JobEditInputModel form = world.EditFormOf(job);
            form.Source = source;
            form.PhoneNumber = "07700 900999";

            JobEditResult result = await world.Bookings.EditDetailsAsync(form);

            Assert.Equal(JobEditOutcome.SourceNeeded, result.Outcome);
            Booking after = world.DbContext.Bookings.Single();
            Assert.Equal(BookingSource.Phone, after.Source);
            Assert.Equal("07700 900456", after.PhoneNumber);
        }

        // A job taken by phone often has no service when it is written in. Picked afterwards,
        // its price is the estimate, as it would have been had it been picked at the start.
        [Fact]
        public async Task PickingAServiceOnAJobThatHadNoneMakesItsPriceTheEstimate()
        {
            using var world = new BookingWorld();
            Booking job = await world.SeedWrittenInJobAsync();
            JobEditInputModel form = world.EditFormOf(job);
            form.ServiceId = world.ServiceId;

            JobEditResult result = await world.Bookings.EditDetailsAsync(form);

            Assert.Equal(JobEditOutcome.Saved, result.Outcome);
            Assert.Equal(new[] { "service" }, result.Changed);

            Booking after = world.DbContext.Bookings.Include(x => x.BookingServices).Single();
            Assert.Equal(90.00m, after.TotalAmount);
            BookingService line = Assert.Single(after.BookingServices);
            Assert.Equal(world.ServiceId, line.ServiceId);
            Assert.Equal(90.00m, line.PriceAtBooking);
            Assert.Equal("Details changed. Service was not picked.", world.HistoryOf(job).Last());
        }

        // The estimate follows the service. What the job came to and what has been paid were
        // typed in or paid in for the job itself, and are not the form's to change.
        [Fact]
        public async Task ChangingTheServiceChangesTheEstimateAndLeavesTheFinalPriceAndTheMoneyAlone()
        {
            using var world = new BookingWorld();
            Booking booking = await world.SeedPaidBookingAsync();
            await world.Bookings.CompleteBookingAsync(booking.Id, 135.00m);
            Service other = world.AddService("Tap Repair", 60.00m);
            JobEditInputModel form = world.EditFormOf(booking);
            form.ServiceId = other.Id;

            JobEditResult result = await world.Bookings.EditDetailsAsync(form);

            Assert.Equal(JobEditOutcome.Saved, result.Outcome);

            Booking after = world.DbContext.Bookings.Include(x => x.BookingServices).Single();
            Assert.Equal(60.00m, after.TotalAmount);
            Assert.Equal(135.00m, after.FinalPrice);
            Assert.Equal(50.00m, after.DepositAmount);
            BookingService line = Assert.Single(after.BookingServices);
            Assert.Equal(other.Id, line.ServiceId);
            Assert.Equal(60.00m, line.PriceAtBooking);
            Assert.Equal(new[] { "DepositPaid" }, world.PaymentStatusesOf(booking));
            Assert.Equal("Details changed. Service was Leak Fix.", world.HistoryOf(booking).Last());
        }

        [Fact]
        public async Task TakingTheServiceOffTakesTheEstimateWithIt()
        {
            using var world = new BookingWorld();
            Booking booking = await world.SeedPaidBookingAsync();
            JobEditInputModel form = world.EditFormOf(booking);
            form.ServiceId = null;

            JobEditResult result = await world.Bookings.EditDetailsAsync(form);

            Assert.Equal(JobEditOutcome.Saved, result.Outcome);
            Assert.Null(world.DbContext.Bookings.Single().TotalAmount);
            Assert.Empty(world.DbContext.BookingServices);
            Assert.Equal("Details changed. Service was Leak Fix.", world.HistoryOf(booking).Last());

            // Nothing a job held is thrown away: the line is hidden, not deleted.
            Assert.True(world.DbContext.BookingServices.IgnoreQueryFilters().Single().IsDeleted);

            // Picked again later, it is a job with one service, not two.
            form.ServiceId = world.ServiceId;
            await world.Bookings.EditDetailsAsync(form);
            Assert.Equal(world.ServiceId, world.DbContext.BookingServices.Single().ServiceId);
            Assert.Equal(90.00m, world.DbContext.Bookings.Single().TotalAmount);
        }

        [Fact]
        public async Task AServiceThatIsNotOnTheListCannotBePicked()
        {
            using var world = new BookingWorld();
            Booking booking = await world.SeedPaidBookingAsync();
            JobEditInputModel form = world.EditFormOf(booking);
            form.ServiceId = Guid.NewGuid();
            form.PhoneNumber = "07700 900999";

            JobEditResult result = await world.Bookings.EditDetailsAsync(form);

            Assert.Equal(JobEditOutcome.ServiceNotFound, result.Outcome);
            Booking after = world.DbContext.Bookings.Single();
            Assert.Equal("07700 900123", after.PhoneNumber);
            Assert.Equal(90.00m, after.TotalAmount);
            Assert.Equal(world.ServiceId, world.DbContext.BookingServices.Single().ServiceId);
        }

        // A service can be deleted in the admin panel after jobs were booked with it. The form
        // then sends back the id the job still holds. That is no change, so the service is not
        // looked up among the ones still offered, and correcting a phone number does not cost
        // the job its service and its estimate.
        [Fact]
        public async Task AJobKeepsAServiceThatWasDeletedSinceWhenItsOtherDetailsAreSaved()
        {
            using var world = new BookingWorld();
            Booking booking = await world.SeedPaidBookingAsync();
            Service deleted = world.DbContext.Services.Single(x => x.Id == world.ServiceId);
            deleted.IsDeleted = true;
            deleted.DeletedOn = DateTime.UtcNow;
            await world.DbContext.SaveChangesAsync();
            JobEditInputModel form = world.EditFormOf(booking);
            form.PhoneNumber = "07700 900999";

            JobEditResult result = await world.Bookings.EditDetailsAsync(form);

            Assert.Equal(JobEditOutcome.Saved, result.Outcome);
            Assert.Equal(new[] { "phone number" }, result.Changed);
            Assert.Equal(world.ServiceId, world.DbContext.BookingServices.Single().ServiceId);
            Assert.Equal(90.00m, world.DbContext.Bookings.Single().TotalAmount);

            // Taken off on purpose, the line still names the service it was.
            form.ServiceId = null;
            await world.Bookings.EditDetailsAsync(form);
            Assert.Equal("Details changed. Service was Leak Fix.", world.HistoryOf(booking).Last());
        }

        // Saving a new email sends nothing. The deposit email, and the one naming the
        // technician, went to the address the job had then; the result says so, so that the
        // admin's page can.
        [Fact]
        public async Task ChangingAWebsiteBookingsEmailSendsNothingAndSaysATechnicianWasAlreadyNamed()
        {
            using var world = new BookingWorld();
            Booking booking = await world.SeedPaidBookingAsync();
            Technician technician = world.AddTechnician("Zapryan");
            await world.Bookings.AssignTechnicianAsync(booking.Id, technician.Id);
            List<SentEmail> sent = world.CaptureEmails();
            JobEditInputModel form = world.EditFormOf(booking);
            form.Email = "ada.lovelace@example.com";

            JobEditResult result = await world.Bookings.EditDetailsAsync(form);

            Assert.Equal(JobEditOutcome.Saved, result.Outcome);
            Assert.Equal(new[] { "email" }, result.Changed);
            Assert.True(result.EmailChangedOnWebsiteBooking);
            Assert.Equal("Zapryan", result.TechnicianName);
            Assert.Equal("ada.lovelace@example.com", world.DbContext.Bookings.Single().Email);
            Assert.Equal("Details changed. Email was ada@example.com.", world.HistoryOf(booking).Last());
            Assert.Empty(sent);
        }

        [Fact]
        public async Task ChangingAWebsiteBookingsEmailBeforeATechnicianIsPickedNamesNobody()
        {
            using var world = new BookingWorld();
            Booking booking = await world.SeedPaidBookingAsync();
            JobEditInputModel form = world.EditFormOf(booking);
            form.Email = "ada.lovelace@example.com";

            JobEditResult result = await world.Bookings.EditDetailsAsync(form);

            Assert.True(result.EmailChangedOnWebsiteBooking);
            Assert.Null(result.TechnicianName);
        }

        // A customer's own words are worth keeping when the admin rewrites them.
        [Fact]
        public async Task ARewrittenDescriptionIsKeptInTheHistory()
        {
            using var world = new BookingWorld();
            Booking booking = await world.SeedPaidBookingAsync();
            JobEditInputModel form = world.EditFormOf(booking);
            form.ProblemDescription = "Kitchen tap drips, and the bath tap too.";

            JobEditResult result = await world.Bookings.EditDetailsAsync(form);

            Assert.Equal(new[] { "what the job is" }, result.Changed);
            Assert.Equal("Kitchen tap drips, and the bath tap too.", world.DbContext.Bookings.Single().ProblemDescription);
            Assert.Equal(
                "Details changed. The job was described as: \"The kitchen tap has been dripping for a week.\"",
                world.HistoryOf(booking).Last());
        }

        [Fact]
        public async Task ADescriptionWrittenWhereThereWasNoneSaysSo()
        {
            using var world = new BookingWorld();
            Booking job = await world.SeedWrittenInJobAsync();
            JobEditInputModel form = world.EditFormOf(job);
            form.ProblemDescription = "Fit a new kitchen tap.";

            await world.Bookings.EditDetailsAsync(form);

            Assert.Equal("Details changed. The job had no description.", world.HistoryOf(job).Last());
        }

        // A history line holds 700 characters in the database, and a description alone may be
        // 3000. With every box changed at once, and each as long as its box allows, the line
        // still fits: SQL Server refuses a longer one, and the save with it.
        [Fact]
        public async Task TheHistoryLineHasRoomForEveryBoxChangedAtOnce()
        {
            using var world = new BookingWorld();
            Service longNamed = world.AddService(new string('s', 100), 60.00m);
            Booking job = await world.SeedWrittenInJobAsync();
            JobEditInputModel form = world.EditFormOf(job);
            form.CustomerFirstName = new string('f', 100);
            form.CustomerLastName = new string('l', 100);
            form.PhoneNumber = new string('1', 20);
            form.Email = new string('e', 243) + "@example.com";
            form.Address = new string('a', 300);
            form.ServiceId = longNamed.Id;
            form.Source = BookingSource.WhatsApp;
            form.ProblemDescription = "It began in the kitchen.\r\n" + new string('d', 2970);
            await world.Bookings.EditDetailsAsync(form);

            form.CustomerFirstName = "Grace";
            form.CustomerLastName = "Hopper";
            form.PhoneNumber = "07700 900999";
            form.Email = "grace@example.com";
            form.Address = "3 Navy Row, Epsom";
            form.ServiceId = world.ServiceId;
            form.Source = BookingSource.Agency;
            form.ProblemDescription = "Fit a new kitchen tap.";
            JobEditResult result = await world.Bookings.EditDetailsAsync(form);

            Assert.Equal(
                new[] { "first name", "last name", "phone number", "email", "address", "service", "where it came from", "what the job is" },
                result.Changed);

            var line = world.HistoryOf(job).Last();
            Assert.True(line.Length <= JobHistory.LineRoom, $"The line is {line.Length} characters long.");
            Assert.StartsWith("Details changed. First name was " + new string('f', 79) + "…. Last name was ", line);
            Assert.Contains(" Where it came from was WhatsApp. The job was described as: \"It began in the kitchen. ddd", line);
            Assert.EndsWith("d…\"", line);
        }

        [Fact]
        public void TextKeptInAHistoryLineIsPutOnOneLineAndCutToTheRoomThereIs()
        {
            Assert.Equal("Tap drips. Bath too.", JobHistory.Shorten("  Tap drips.\r\n\r\n Bath   too. ", 80));
            Assert.Equal("Tap dr…", JobHistory.Shorten("Tap drips. Bath too.", 7));
            Assert.Equal(string.Empty, JobHistory.Shorten(null, 80));
        }

        [Fact]
        public async Task EditingAJobThatDoesNotExistSaysSo()
        {
            using var world = new BookingWorld();

            JobEditResult result = await world.Bookings.EditDetailsAsync(new JobEditInputModel
            {
                Id = Guid.NewGuid(),
                CustomerFirstName = "Grace",
                PhoneNumber = "07700 900456",
                Source = BookingSource.Phone,
            });

            Assert.Equal(JobEditOutcome.BookingNotFound, result.Outcome);
        }
    }
}
