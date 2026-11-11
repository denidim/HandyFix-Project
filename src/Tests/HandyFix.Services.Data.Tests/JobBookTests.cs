namespace HandyFix.Services.Data.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Common;
    using HandyFix.Data.Models;
    using HandyFix.Services.Data.Invoices;
    using HandyFix.Services.Data.Payments;
    using HandyFix.Web.ViewModels.Booking;

    using Microsoft.EntityFrameworkCore;

    using Xunit;

    // The Job Book (PROJECT_STATE.md Section 3ce): every job the business does has a page,
    // whether it was booked on the website with a deposit or written in by the admin. These are
    // the rules behind that page: what a written-in job is, what cancelling keeps, how a job is
    // finished and paid for, how it is moved, and the line each of those leaves in its history.
    public class JobBookTests
    {
        // A job taken by phone has a first name and a number, and often nothing else. The
        // booking's table used to demand a last name, an email, an address and a description.
        [Fact]
        public async Task AWrittenInJobNeedsOnlyANameAPhoneADayAndATime()
        {
            using var world = new BookingWorld();
            DateTime start = DateTime.Today.AddDays(2).AddHours(14);

            Guid id = await world.Jobs.CreateWrittenInJobAsync(world.WrittenInJob(start, BookingSource.WhatsApp));

            Booking job = world.DbContext.Bookings.Include(x => x.Status).Single(x => x.Id == id);
            Assert.Equal("Grace", job.CustomerFirstName);
            Assert.Null(job.CustomerLastName);
            Assert.Null(job.Email);
            Assert.Null(job.Address);
            Assert.Null(job.ProblemDescription);
            Assert.Equal(BookingSource.WhatsApp, job.Source);
            Assert.Equal(start, job.ScheduledStart);
            Assert.Equal(start.AddHours(1), job.ScheduledEnd);
            Assert.Null(job.TotalAmount);
            Assert.Null(job.DepositAmount);

            // Booked from the start: there is no deposit to wait for.
            Assert.Equal("Approved", job.Status.Name);
            Assert.Equal(JobLabels.Booked, JobLabels.Job(job.Status.Name));
            Assert.Equal(new[] { "Written in by the admin. Came from: WhatsApp." }, world.HistoryOf(job));
        }

        // The admin blocks the hour in the calendar by hand, as agreed: a written-in job claims
        // no slot, even when one is free at exactly its time. And nobody is emailed.
        [Fact]
        public async Task AWrittenInJobTakesNoSlotAndSendsNoEmail()
        {
            using var world = new BookingWorld();
            List<SentEmail> sent = world.CaptureEmails();
            JobInputModel form = world.WrittenInJob(world.SlotStart);
            form.Email = "grace@example.com";

            await world.Jobs.CreateWrittenInJobAsync(form);

            Assert.False(world.Slot(world.SlotId).IsBooked);
            Assert.Null(world.Slot(world.SlotId).BookingId);
            Assert.Empty(sent);
        }

        [Fact]
        public async Task AWrittenInJobWithAServiceTakesItsPriceAsTheEstimate()
        {
            using var world = new BookingWorld();
            JobInputModel form = world.WrittenInJob();
            form.ServiceId = world.ServiceId;
            form.CustomerLastName = "  Hopper ";
            form.Address = " 3 Navy Row, Epsom ";

            Guid id = await world.Jobs.CreateWrittenInJobAsync(form);

            Booking job = world.DbContext.Bookings.Include(x => x.BookingServices).Single(x => x.Id == id);
            Assert.Equal(90.00m, job.TotalAmount);
            Assert.Equal(world.ServiceId, Assert.Single(job.BookingServices).ServiceId);
            Assert.Equal("Hopper", job.CustomerLastName);
            Assert.Equal("3 Navy Row, Epsom", job.Address);
        }

        // The website is not on the form's list. A "website" job made by hand would have no
        // deposit and no slot, and would sit in the list looking like a paid booking's twin.
        [Fact]
        public async Task AWrittenInJobCannotClaimToComeFromTheWebsite()
        {
            using var world = new BookingWorld();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => world.Jobs.CreateWrittenInJobAsync(world.WrittenInJob(source: BookingSource.Website)));

            Assert.Empty(world.DbContext.Bookings);
        }

        // The sweep abandons website bookings whose deposit never came, fifteen minutes after
        // they were made. A job the admin wrote in has no deposit to come and must be left alone.
        [Fact]
        public async Task TheSweepThatAbandonsUnpaidBookingsLeavesAWrittenInJobAlone()
        {
            using var world = new BookingWorld();
            Booking job = await world.SeedWrittenInJobAsync();
            job.CreatedOn = DateTime.UtcNow.AddHours(-3);
            await world.DbContext.SaveChangesAsync();

            var abandoned = await world.Bookings.ReleaseAbandonedBookingsAsync(TimeSpan.FromMinutes(15));

            Assert.Equal(0, abandoned);
            Assert.Equal("Approved", world.StatusOf(job));
        }

        // From the technician on, a written-in job goes the way a website booking does: the
        // customer is emailed who is coming (Section 3dt). The job was written in with no
        // service and no address, and the email leaves those rows out rather than send them empty.
        [Fact]
        public async Task PickingATechnicianOnAWrittenInJobNeedsNoDepositAndEmailsTheCustomer()
        {
            using var world = new BookingWorld();
            JobInputModel form = world.WrittenInJob();
            form.Email = "grace@example.com";
            Guid id = await world.Jobs.CreateWrittenInJobAsync(form);
            Booking job = world.DbContext.Bookings.Single(x => x.Id == id);
            Technician technician = world.AddTechnician("Zapryan");
            List<SentEmail> sent = world.CaptureEmails();

            TechnicianAssignmentResult result = await world.Jobs.AssignTechnicianAsync(id, technician.Id);

            Assert.Equal(TechnicianAssignmentOutcome.AssignedAndCustomerEmailed, result.Outcome);
            Assert.Equal("Zapryan", result.TechnicianName);
            Assert.Equal(technician.Id, world.DbContext.Bookings.Single().TechnicianId);
            SentEmail email = Assert.Single(sent);
            Assert.Equal("grace@example.com", email.To);
            Assert.Equal("Your technician for your Plumbing Handyman Surrey booking", email.Subject);
            Assert.Contains("<strong>Zapryan</strong>", email.Body);
            Assert.Contains(BookingReference.Short(id), email.Body);
            Assert.DoesNotContain("Service(s)", email.Body);
            Assert.DoesNotContain("Address", email.Body);
            Assert.Equal("Technician picked: Zapryan. The customer was emailed.", world.HistoryOf(job).Last());
        }

        // A job taken by phone may have no email address. The technician is saved all the same,
        // and the admin is the one to tell the customer who is coming.
        [Fact]
        public async Task PickingATechnicianOnAJobWithNoEmailAddressSavesItAndSendsNothing()
        {
            using var world = new BookingWorld();
            Booking job = await world.SeedWrittenInJobAsync();
            Technician technician = world.AddTechnician("Zapryan");
            List<SentEmail> sent = world.CaptureEmails();

            TechnicianAssignmentResult result = await world.Jobs.AssignTechnicianAsync(job.Id, technician.Id);

            Assert.Equal(TechnicianAssignmentOutcome.AssignedNoEmailAddress, result.Outcome);
            Assert.Equal("Zapryan", result.TechnicianName);
            Assert.Equal(technician.Id, world.DbContext.Bookings.Single().TechnicianId);
            Assert.Empty(sent);
            Assert.Empty(world.DbContext.EmailsNotSent);
            Assert.Equal("Technician picked: Zapryan. The job has no email address, so the customer was not emailed.", world.HistoryOf(job).Last());
        }

        [Fact]
        public async Task AWebsiteBookingKeepsItsOwnDateAndStartsItsHistory()
        {
            using var world = new BookingWorld();

            BookingCreateResult result = await world.Bookings.CreateBookingAsync(world.BookingFor("12 Main Rd, Sutton"), new List<string>());

            Booking booking = world.DbContext.Bookings.Single(x => x.Id == result.BookingId);
            Assert.Equal(BookingSource.Website, booking.Source);
            Assert.Equal(world.SlotStart, booking.ScheduledStart);
            Assert.Equal(world.SlotStart.AddHours(1), booking.ScheduledEnd);
            Assert.Equal(new[] { "Booked on the website." }, world.HistoryOf(booking));
        }

        // The webhook and the customer's own return from the payment page both report the
        // deposit. It is one deposit and leaves one line.
        [Fact]
        public async Task TheDepositLeavesOneHistoryLineHoweverOftenItIsReported()
        {
            using var world = new BookingWorld();
            var checkoutSessionId = await world.SeedPendingPaymentAsync("12 Main Rd, Sutton");
            Booking booking = world.DbContext.Bookings.Single();

            await world.Payments.ProcessPaymentSuccessAsync(checkoutSessionId, "txn_1");
            await world.Payments.ProcessPaymentSuccessAsync(checkoutSessionId, "txn_1");

            Assert.Equal(new[] { "Deposit of £50.00 paid on the website." }, world.HistoryOf(booking));
        }

        [Theory]
        [InlineData(true, "Technician picked: Zapryan. The customer was emailed.")]
        [InlineData(false, "Technician picked: Zapryan. The email to the customer could not be sent.")]
        public async Task PickingATechnicianLeavesALineThatSaysWhetherTheCustomerWasTold(bool emailGoes, string expectedLine)
        {
            using var world = new BookingWorld();
            Booking booking = await world.SeedPaidBookingAsync();
            Technician technician = world.AddTechnician("Zapryan");
            if (!emailGoes)
            {
                world.EmailsFail();
            }

            await world.Jobs.AssignTechnicianAsync(booking.Id, technician.Id);
            await world.Jobs.AssignTechnicianAsync(booking.Id, null);

            List<string> history = world.HistoryOf(booking);
            Assert.Equal(expectedLine, history[history.Count - 2]);
            Assert.Equal("Technician taken off (was Zapryan).", history.Last());
        }

        // A cancelled booking used to lose its date: the time lived on the slot it gave back,
        // and the list showed "Jan 01, 0001" for it.
        [Fact]
        public async Task ACancelledJobKeepsItsDateAndTheReason()
        {
            using var world = new BookingWorld();
            Booking booking = await world.SeedPaidBookingAsync();

            var cancelled = await world.Jobs.CancelBookingAsync(booking.Id, "  The customer rang: the tap was fixed by a neighbour.  ");

            Assert.Equal(JobCancelOutcome.Cancelled, cancelled);
            Booking after = world.DbContext.Bookings.Single();
            Assert.Equal("Cancelled", world.StatusOf(booking));
            Assert.Equal("The customer rang: the tap was fixed by a neighbour.", after.CancelReason);
            Assert.Equal(world.SlotStart, after.ScheduledStart);
            Assert.False(world.Slot(world.SlotId).IsBooked);
            Assert.Equal("Cancelled. Reason: The customer rang: the tap was fixed by a neighbour.", world.HistoryOf(booking).Last());
        }

        // A booking made before jobs kept their own date still has it on its slot. Cancelling
        // copies it across in the last moment the slot says whose it was.
        [Fact]
        public async Task CancellingABookingFromBeforeJobsKeptTheirDateTakesItFromTheSlot()
        {
            using var world = new BookingWorld();
            Booking booking = await world.SeedPaidBookingAsync();
            booking.ScheduledStart = null;
            booking.ScheduledEnd = null;
            await world.DbContext.SaveChangesAsync();

            await world.Jobs.CancelBookingAsync(booking.Id, "Called off.");

            Booking after = world.DbContext.Bookings.Single();
            Assert.Equal(world.SlotStart, after.ScheduledStart);
            Assert.Equal(world.SlotStart.AddHours(1), after.ScheduledEnd);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task CancellingNeedsAReason(string reason)
        {
            using var world = new BookingWorld();
            Booking booking = await world.SeedPaidBookingAsync();

            var cancelled = await world.Jobs.CancelBookingAsync(booking.Id, reason);

            Assert.Equal(JobCancelOutcome.NotAllowed, cancelled);
            Assert.Equal("Approved", world.StatusOf(booking));
            Assert.True(world.Slot(world.SlotId).IsBooked);
        }

        [Fact]
        public async Task AnAbandonedBookingKeepsItsDateAndSaysWhy()
        {
            using var world = new BookingWorld();
            await world.SeedPendingPaymentAsync("12 Main Rd, Sutton");
            Booking booking = world.DbContext.Bookings.Single();
            booking.CreatedOn = DateTime.UtcNow.AddMinutes(-20);
            booking.ScheduledStart = null;
            booking.ScheduledEnd = null;
            await world.DbContext.SaveChangesAsync();

            var abandoned = await world.Bookings.ReleaseAbandonedBookingsAsync(TimeSpan.FromMinutes(15));

            Assert.Equal(1, abandoned);
            Assert.Equal("Abandoned", world.StatusOf(booking));
            Assert.Equal(world.SlotStart, world.DbContext.Bookings.Single().ScheduledStart);
            Assert.False(world.Slot(world.SlotId).IsBooked);
            Assert.Equal("Abandoned: the deposit was not paid in time. Its hour went back on sale.", world.HistoryOf(booking).Last());
        }

        // Refunds are made by hand in Stripe. The tick is the record that one was, and a deposit
        // that went back is no longer money in.
        [Fact]
        public async Task TickingDepositRefundedOnACancelledJobTakesTheDepositOutOfTheMoneyIn()
        {
            using var world = new BookingWorld();
            Booking booking = await world.SeedPaidBookingAsync();
            await world.Jobs.CancelBookingAsync(booking.Id, "Called off two days before.");

            var ticked = await world.Payments.SetDepositRefundedAsync(booking.Id, true);

            Assert.True(ticked);
            Assert.Equal(new[] { "Refunded" }, world.PaymentStatusesOf(booking));
            Assert.Equal(0m, await world.Payments.GetTotalRevenueAsync());
            Assert.Equal("Deposit marked as refunded.", world.HistoryOf(booking).Last());

            // Ticked twice it stays ticked, and says so once.
            Assert.True(await world.Payments.SetDepositRefundedAsync(booking.Id, true));
            Assert.Single(world.HistoryOf(booking), line => line == "Deposit marked as refunded.");

            // Ticked by mistake, it can be unticked.
            Assert.True(await world.Payments.SetDepositRefundedAsync(booking.Id, false));
            Assert.Equal(new[] { "DepositPaid" }, world.PaymentStatusesOf(booking));
            Assert.Equal("Deposit marked as not refunded.", world.HistoryOf(booking).Last());
        }

        [Fact]
        public async Task DepositRefundedCannotBeTickedOnAJobThatIsNotCancelled()
        {
            using var world = new BookingWorld();
            Booking booking = await world.SeedPaidBookingAsync();

            var ticked = await world.Payments.SetDepositRefundedAsync(booking.Id, true);

            Assert.False(ticked);
            Assert.Equal(new[] { "DepositPaid" }, world.PaymentStatusesOf(booking));
        }

        [Fact]
        public async Task DepositRefundedCannotBeTickedWhereNoDepositWasPaid()
        {
            using var world = new BookingWorld();
            Booking job = await world.SeedWrittenInJobAsync();
            await world.Jobs.CancelBookingAsync(job.Id, "Called off.");

            Assert.False(await world.Payments.SetDepositRefundedAsync(job.Id, true));
        }

        [Fact]
        public async Task MarkingAJobDoneWritesItsFinalPriceOnIt()
        {
            using var world = new BookingWorld();
            Booking booking = await world.SeedPaidBookingAsync();

            var done = await world.Jobs.CompleteBookingAsync(booking.Id, 135.00m);

            Assert.True(done);
            Assert.Equal("Completed", world.StatusOf(booking));
            Assert.Equal(135.00m, world.DbContext.Bookings.Single().FinalPrice);
            Assert.Equal("Marked done. Final price £135.00.", world.HistoryOf(booking).Last());
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        [InlineData(100000.01)]
        public async Task AJobIsNotMarkedDoneWithoutAPrice(double finalPrice)
        {
            using var world = new BookingWorld();
            Booking booking = await world.SeedPaidBookingAsync();

            var done = await world.Jobs.CompleteBookingAsync(booking.Id, (decimal)finalPrice);

            Assert.False(done);
            Assert.Equal("Approved", world.StatusOf(booking));
            Assert.Null(world.DbContext.Bookings.Single().FinalPrice);
        }

        [Fact]
        public async Task AWrittenInJobIsMarkedDoneWithNoDeposit()
        {
            using var world = new BookingWorld();
            Booking job = await world.SeedWrittenInJobAsync();

            Assert.True(await world.Jobs.CompleteBookingAsync(job.Id, 60.00m));
            Assert.Equal("Completed", world.StatusOf(job));
        }

        [Fact]
        public async Task TheFinalPriceCanBePutRightOnceTheJobIsDoneAndNotBefore()
        {
            using var world = new BookingWorld();
            Booking booking = await world.SeedPaidBookingAsync();

            Assert.False(await world.Jobs.ChangeFinalPriceAsync(booking.Id, 140.00m));
            Assert.Null(world.DbContext.Bookings.Single().FinalPrice);

            await world.Jobs.CompleteBookingAsync(booking.Id, 135.00m);

            Assert.True(await world.Jobs.ChangeFinalPriceAsync(booking.Id, 140.00m));
            Assert.Equal(140.00m, world.DbContext.Bookings.Single().FinalPrice);
            Assert.Equal("Final price changed from £135.00 to £140.00.", world.HistoryOf(booking).Last());

            // Saved as it stands, it leaves no line.
            var lines = world.HistoryOf(booking).Count;
            Assert.True(await world.Jobs.ChangeFinalPriceAsync(booking.Id, 140.00m));
            Assert.Equal(lines, world.HistoryOf(booking).Count);

            Assert.False(await world.Jobs.ChangeFinalPriceAsync(booking.Id, 0m));
        }

        [Theory]
        [InlineData("Card")]
        [InlineData("Cash")]
        [InlineData("Bank transfer")]
        public async Task APaymentIsWrittenOnTheJobWithHowItWasPaid(string method)
        {
            using var world = new BookingWorld();
            Booking booking = await world.SeedPaidBookingAsync();
            await world.Jobs.CompleteBookingAsync(booking.Id, 135.00m);
            await world.Invoices.MakeAsync(booking.Id, null);

            PaymentAddResult added = await world.Payments.AddPaymentAsync(booking.Id, 85.00m, method);

            Assert.True(added.Added);
            Payment payment = world.DbContext.Payments.Include(x => x.Status).Single(x => x.Method != null);
            Assert.Equal(85.00m, payment.Amount);
            Assert.Equal(method, payment.Method);
            Assert.Equal("Completed", payment.Status.Name);
            Assert.Contains($"Payment of £85.00 written on the job: {method}.", world.HistoryOf(booking));

            // It paid the job in full, so the invoice marked paid went to the customer by itself.
            Assert.Equal(InvoiceEmailOutcome.Sent, added.PaidInvoice);
            Assert.Equal("Invoice PHS-0001 emailed to the customer: paid in full.", world.HistoryOf(booking).Last());

            // The deposit and the payment together are what the business took.
            Assert.Equal(135.00m, await world.Payments.GetTotalRevenueAsync());

            List<PaymentLineViewModel> list = (await world.Payments.GetMoneyListAsync<PaymentLineViewModel>(booking.Id)).ToList();
            Assert.Equal(2, list.Count);
            Assert.Equal("Deposit, card on the website", list[0].Description);
            Assert.True(list[0].IsWebsiteDeposit);
            Assert.Equal(method, list[1].Description);
            Assert.False(list[1].IsWebsiteDeposit);
        }

        // A done job takes a payment only once its invoice is made: the admin pressed Add Payment
        // before sending the invoice on staging (Section 3ds), because the form came first.
        [Fact]
        public async Task ADoneJobsPaymentWaitsForItsInvoice()
        {
            using var world = new BookingWorld();
            Booking booking = await world.SeedPaidBookingAsync();
            await world.Jobs.CompleteBookingAsync(booking.Id, 135.00m);

            Assert.False((await world.Payments.AddPaymentAsync(booking.Id, 85.00m, "Cash")).Added);
            Assert.Equal(new[] { "DepositPaid" }, world.PaymentStatusesOf(booking));

            await world.Invoices.MakeAsync(booking.Id, null);

            Assert.True((await world.Payments.AddPaymentAsync(booking.Id, 85.00m, "Cash")).Added);
        }

        // Paid in part, nothing goes by itself: the admin sends the invoice again if they wish.
        [Fact]
        public async Task APartPaymentSendsNoInvoiceByItself()
        {
            using var world = new BookingWorld();
            List<SentEmail> sent = world.CaptureEmails();
            Booking booking = await world.SeedPaidBookingAsync();
            await world.Jobs.CompleteBookingAsync(booking.Id, 135.00m);
            await world.Invoices.MakeAsync(booking.Id, null);
            var before = sent.Count;

            PaymentAddResult added = await world.Payments.AddPaymentAsync(booking.Id, 40.00m, "Cash");

            Assert.True(added.Added);
            Assert.Null(added.PaidInvoice);
            Assert.Equal(before, sent.Count);
        }

        // A job that is booked and not done yet has no invoice, and still takes money paid ahead.
        [Fact]
        public async Task ABookedJobTakesAPaymentWithNoInvoice()
        {
            using var world = new BookingWorld();
            Booking booking = await world.SeedWrittenInJobAsync();

            PaymentAddResult added = await world.Payments.AddPaymentAsync(booking.Id, 50.00m, "Bank transfer");

            Assert.True(added.Added);
            Assert.Null(added.PaidInvoice);
        }

        [Theory]
        [InlineData(0, "Cash")]
        [InlineData(-10, "Cash")]
        [InlineData(100000.01, "Cash")]
        [InlineData(85, "Cheque")]
        [InlineData(85, null)]
        public async Task APaymentWithNoAmountOrAnUnknownWayOfPayingIsNotWritten(double amount, string method)
        {
            using var world = new BookingWorld();
            Booking booking = await world.SeedPaidBookingAsync();

            var added = await world.Payments.AddPaymentAsync(booking.Id, (decimal)amount, method);

            Assert.False(added.Added);
            Assert.Equal(new[] { "DepositPaid" }, world.PaymentStatusesOf(booking));
        }

        [Theory]
        [InlineData("Cancelled")]
        [InlineData("Abandoned")]
        public async Task APaymentIsNotWrittenOnAJobThatFellAway(string status)
        {
            using var world = new BookingWorld();
            Booking booking = await world.SeedPaidBookingAsync();
            world.SetStatus(booking, status);

            Assert.False((await world.Payments.AddPaymentAsync(booking.Id, 85.00m, "Cash")).Added);
        }

        // A website booking's first payment is its deposit, paid on the website and nowhere else.
        [Fact]
        public async Task APaymentIsNotWrittenOnAWebsiteBookingBeforeItsDeposit()
        {
            using var world = new BookingWorld();
            await world.SeedPendingPaymentAsync("12 Main Rd, Sutton");
            Booking booking = world.DbContext.Bookings.Single();

            Assert.False((await world.Payments.AddPaymentAsync(booking.Id, 50.00m, "Cash")).Added);
        }

        [Fact]
        public async Task APaymentWrittenByMistakeCanBeTakenOffAndTheDepositCannot()
        {
            using var world = new BookingWorld();
            Booking booking = await world.SeedPaidBookingAsync();
            await world.Payments.AddPaymentAsync(booking.Id, 85.00m, "Cash");
            Guid written = world.DbContext.Payments.Single(x => x.Method == "Cash").Id;
            Guid deposit = world.DbContext.Payments.Single(x => x.Method == null).Id;

            Assert.False(await world.Payments.RemovePaymentAsync(booking.Id, deposit));
            Assert.True(await world.Payments.RemovePaymentAsync(booking.Id, written));

            Assert.Equal(new[] { "DepositPaid" }, world.PaymentStatusesOf(booking));
            Assert.Equal(50.00m, await world.Payments.GetTotalRevenueAsync());
            Assert.Equal("Payment of £85.00 (Cash) taken off the job.", world.HistoryOf(booking).Last());

            // Not another job's payment by its id.
            Assert.False(await world.Payments.RemovePaymentAsync(Guid.NewGuid(), deposit));
        }

        // "Pending" is a website booking that has not paid its deposit. It read "Booked", the same
        // as a job the admin had written in, and the two could not be told apart in the list
        // (PROJECT_STATE.md Section 3ch).
        [Theory]
        [InlineData("Pending", "Waiting for deposit")]
        [InlineData("Approved", "Booked")]
        [InlineData("InProgress", "Booked")]
        [InlineData("Completed", "Done")]
        [InlineData("Cancelled", "Cancelled")]
        [InlineData("Abandoned", "Abandoned")]
        public void TheJobLabelSaysWhereTheWorkStands(string statusName, string label)
        {
            Assert.Equal(label, JobLabels.Job(statusName));
            Assert.Contains(statusName, JobLabels.StatusNames(label));
        }

        [Theory]
        [InlineData(0.0, -1.0, false, false, false, "Not paid")]
        [InlineData(0.0, 135.0, false, false, false, "Not paid")]
        [InlineData(0.0, -1.0, false, false, true, "Deposit refunded")]
        [InlineData(50.0, -1.0, true, false, false, "Deposit paid")]
        [InlineData(50.0, 135.0, true, false, false, "Deposit paid")]
        [InlineData(135.0, 135.0, true, true, false, "Paid in full")]
        [InlineData(140.0, 135.0, true, true, false, "Paid in full")]
        [InlineData(60.0, 60.0, false, true, false, "Paid in full")]
        [InlineData(50.0, 50.0, true, false, false, "Paid in full")]
        [InlineData(100.0, 135.0, true, true, false, "Part paid")]
        [InlineData(30.0, 60.0, false, true, false, "Part paid")]
        [InlineData(60.0, -1.0, false, true, false, "Part paid")]
        public void TheMoneyLabelSaysWhereTheMoneyStands(double paid, double finalPriceOrMinusOneForNone, bool depositPaid, bool otherPayments, bool depositRefunded, string label)
        {
            decimal? finalPrice = finalPriceOrMinusOneForNone < 0 ? null : (decimal)finalPriceOrMinusOneForNone;

            Assert.Equal(label, JobLabels.Money((decimal)paid, finalPrice, depositPaid, otherPayments, depositRefunded));
        }

        [Fact]
        public async Task TheListsFilterOffersTheJobLabelsInTheOrderAJobGoesThroughThem()
        {
            using var world = new BookingWorld();

            Assert.Equal(new[] { "Waiting for deposit", "Booked", "Done", "Cancelled", "Abandoned" }, await world.Bookings.GetStatusOptionsAsync());
            Assert.DoesNotContain("Pending", JobLabels.StatusNames("Booked"));
        }

        // The calendar's jobs arrows lead to the nearest day with a job, however far away. They
        // asked about the next day only, so a job three days away left the arrow grey
        // (PROJECT_STATE.md Section 3cs). They read what a day's own list reads, so a lit arrow
        // never opens an empty list: a job that was called off counts for nothing.
        [Fact]
        public async Task TheNearestDayWithJobsIsFoundHoweverFarAwayItIs()
        {
            using var world = new BookingWorld();
            DateTime day = world.SlotStart.Date;
            Assert.Null(await world.Bookings.GetNextDayWithJobsAsync(day));
            Assert.Null(await world.Bookings.GetPreviousDayWithJobsAsync(day));

            Booking near = await world.SeedWrittenInJobAsync(day.AddDays(3).AddHours(15));
            await world.SeedWrittenInJobAsync(day.AddDays(10).AddHours(9));

            Assert.Equal(day.AddDays(3), await world.Bookings.GetNextDayWithJobsAsync(day));
            Assert.Equal(day.AddDays(3), await world.Bookings.GetNextDayWithJobsAsync(day.AddHours(23)));
            Assert.Equal(day.AddDays(10), await world.Bookings.GetNextDayWithJobsAsync(day.AddDays(3)));
            Assert.Null(await world.Bookings.GetNextDayWithJobsAsync(day.AddDays(10)));
            Assert.Equal(day.AddDays(3), await world.Bookings.GetPreviousDayWithJobsAsync(day.AddDays(10)));
            Assert.Null(await world.Bookings.GetPreviousDayWithJobsAsync(day.AddDays(3)));

            await world.Jobs.CancelBookingAsync(near.Id, "Called off.");

            Assert.Equal(day.AddDays(10), await world.Bookings.GetNextDayWithJobsAsync(day));
            Assert.Empty(await world.Bookings.GetJobsForDayAsync<BookingDetailsViewModel>(day.AddDays(3)));
        }

        // The slots arrows: any slot counts, on sale or not, because the list under them shows
        // them all. A Saturday's arrow leads on to the Monday, where it stood grey towards the
        // Sunday.
        [Fact]
        public async Task TheNearestDayWithSlotsIsFoundWhateverStateTheyAreIn()
        {
            using var world = new BookingWorld();
            DateTime day = world.SlotStart.Date.AddDays(30);
            Assert.Null(await world.Availability.GetNextDayWithSlotsAsync(day));

            world.AddSlot(day.AddDays(2).AddHours(9), blocked: true);

            Assert.Equal(day.AddDays(2), await world.Availability.GetNextDayWithSlotsAsync(day));
            Assert.Equal(day.AddDays(2), await world.Availability.GetNextDayWithSlotsAsync(day.AddHours(17)));
            Assert.Null(await world.Availability.GetNextDayWithSlotsAsync(day.AddDays(2)));
            Assert.Equal(day.AddDays(2), await world.Availability.GetPreviousDayWithSlotsAsync(day.AddDays(5)));

            // Back from there, past the empty days, to the world's own slot.
            Assert.Equal(world.SlotStart.Date, await world.Availability.GetPreviousDayWithSlotsAsync(day.AddDays(2)));
        }

        // A website booking's deposit promised the time, so it carries its hour in the calendar
        // with it: the hour it leaves goes back on sale and the new one comes off.
        [Fact]
        public async Task MovingAWebsiteBookingGivesItsHourBackAndTakesTheNewOne()
        {
            using var world = new BookingWorld();
            Booking booking = await world.SeedPaidBookingAsync();
            DateTime newStart = world.SlotStart.AddDays(1).AddHours(2);
            AvailabilitySlot newSlot = world.AddSlot(newStart);

            JobMoveResult result = await world.Jobs.MoveBookingAsync(booking.Id, newStart);

            Assert.Equal(JobMoveOutcome.Moved, result.Outcome);
            Assert.True(result.OldHourFreed);
            Assert.True(result.NewHourTaken);
            Assert.False(result.NewHourNotAvailable);

            Assert.False(world.Slot(world.SlotId).IsBooked);
            Assert.Null(world.Slot(world.SlotId).BookingId);
            Assert.True(world.Slot(newSlot.Id).IsBooked);
            Assert.Equal(booking.Id, world.Slot(newSlot.Id).BookingId);

            Booking after = world.DbContext.Bookings.Single();
            Assert.Equal(newStart, after.ScheduledStart);
            Assert.Equal(newStart.AddHours(1), after.ScheduledEnd);
            Assert.Equal("Approved", world.StatusOf(booking));
            Assert.StartsWith("Moved from ", world.HistoryOf(booking).Last());
            Assert.Contains(" to " + newStart.ToString("ddd d MMM yyyy 'at' HH:mm", CultureInfo.InvariantCulture) + ".", world.HistoryOf(booking).Last());
        }

        // The new hour is not the booking's to take when the admin has blocked it, when another
        // booking holds it, or when the calendar has no slot at that time at all. The job still
        // moves; the page tells the admin nothing was taken off sale.
        [Theory]
        [InlineData("blocked")]
        [InlineData("held")]
        [InlineData("missing")]
        public async Task MovingAWebsiteBookingToAnHourTheCalendarCannotGiveMovesItAndTakesNothing(string newHour)
        {
            using var world = new BookingWorld();
            Booking booking = await world.SeedPaidBookingAsync();
            DateTime newStart = world.SlotStart.AddDays(1).AddHours(2);
            Guid someoneElse = Guid.NewGuid();
            AvailabilitySlot newSlot = newHour switch
            {
                "blocked" => world.AddSlot(newStart, blocked: true),
                "held" => world.AddSlot(newStart, heldBy: someoneElse),
                _ => null,
            };

            JobMoveResult result = await world.Jobs.MoveBookingAsync(booking.Id, newStart);

            Assert.Equal(JobMoveOutcome.Moved, result.Outcome);
            Assert.True(result.OldHourFreed);
            Assert.False(result.NewHourTaken);
            Assert.True(result.NewHourNotAvailable);
            Assert.Equal(newStart, world.DbContext.Bookings.Single().ScheduledStart);
            Assert.False(world.Slot(world.SlotId).IsBooked);

            if (newHour == "blocked")
            {
                Assert.False(world.Slot(newSlot.Id).IsBooked);
                Assert.True(world.Slot(newSlot.Id).IsBlocked);
            }
            else if (newHour == "held")
            {
                Assert.Equal(someoneElse, world.Slot(newSlot.Id).BookingId);
            }
        }

        // A written-in job holds no hour, when it is made or when it is moved.
        [Fact]
        public async Task MovingAWrittenInJobChangesNothingInTheCalendar()
        {
            using var world = new BookingWorld();
            Booking job = await world.SeedWrittenInJobAsync();

            JobMoveResult result = await world.Jobs.MoveBookingAsync(job.Id, world.SlotStart);

            Assert.Equal(JobMoveOutcome.Moved, result.Outcome);
            Assert.False(result.OldHourFreed);
            Assert.False(result.NewHourTaken);
            Assert.False(result.NewHourNotAvailable);
            Assert.False(world.Slot(world.SlotId).IsBooked);
            Assert.Equal(world.SlotStart, world.DbContext.Bookings.Single().ScheduledStart);
        }

        [Fact]
        public async Task MovingAJobToWhereItAlreadyIsChangesNothing()
        {
            using var world = new BookingWorld();
            Booking booking = await world.SeedPaidBookingAsync();
            var lines = world.HistoryOf(booking).Count;

            JobMoveResult result = await world.Jobs.MoveBookingAsync(booking.Id, world.SlotStart);

            Assert.Equal(JobMoveOutcome.Unchanged, result.Outcome);
            Assert.True(world.Slot(world.SlotId).IsBooked);
            Assert.Equal(lines, world.HistoryOf(booking).Count);
        }

        [Theory]
        [InlineData("Pending")]
        [InlineData("Completed")]
        [InlineData("Cancelled")]
        [InlineData("Abandoned")]
        public async Task AJobThatIsNotOnCannotBeMoved(string status)
        {
            using var world = new BookingWorld();
            Booking booking = await world.SeedPaidBookingAsync();
            world.SetStatus(booking, status);

            JobMoveResult result = await world.Jobs.MoveBookingAsync(booking.Id, world.SlotStart.AddDays(3));

            Assert.Equal(JobMoveOutcome.NotAllowed, result.Outcome);
            Assert.Equal(world.SlotStart, world.DbContext.Bookings.Single().ScheduledStart);
            Assert.True(world.Slot(world.SlotId).IsBooked);
        }

        [Fact]
        public async Task MovingAJobThatDoesNotExistSaysSo()
        {
            using var world = new BookingWorld();

            Assert.Equal(JobMoveOutcome.BookingNotFound, (await world.Jobs.MoveBookingAsync(Guid.NewGuid(), DateTime.Today)).Outcome);
        }

        [Fact]
        public async Task TheNotesAreSavedOnTheJobAndLeaveNoHistoryLine()
        {
            using var world = new BookingWorld();
            Booking job = await world.SeedWrittenInJobAsync();
            var lines = world.HistoryOf(job).Count;

            Assert.True(await world.Jobs.SaveNotesAsync(job.Id, "  Stopcock is under the kitchen sink.  "));
            Assert.Equal("Stopcock is under the kitchen sink.", world.DbContext.Bookings.Single().AdminNotes);

            Assert.True(await world.Jobs.SaveNotesAsync(job.Id, "   "));
            Assert.Null(world.DbContext.Bookings.Single().AdminNotes);

            Assert.Equal(lines, world.HistoryOf(job).Count);
            Assert.False(await world.Jobs.SaveNotesAsync(Guid.NewGuid(), "Nobody's."));
        }

        [Fact]
        public async Task AJobsHistoryIsGivenOldestFirst()
        {
            using var world = new BookingWorld();
            Booking booking = await world.SeedPaidBookingAsync();
            await world.Jobs.CompleteBookingAsync(booking.Id, 135.00m);

            List<BookingHistoryViewModel> history = (await world.Jobs.GetHistoryAsync<BookingHistoryViewModel>(booking.Id)).ToList();

            Assert.Equal(
                new[] { "Deposit of £50.00 paid on the website.", "Marked done. Final price £135.00." },
                history.Select(x => x.Text));
            Assert.All(history, line => Assert.NotEqual(default, line.CreatedOn));
        }

        // "Unblock" in the admin calendar went through the method that frees a slot from its
        // booking, which leaves the block where it is. A blocked hour, or a day blocked with
        // "Block Entire Day", could never be opened again.
        [Fact]
        public async Task ABlockedHourCanBeOpenedAgain()
        {
            using var world = new BookingWorld();
            await world.Availability.BlockSlotAsync(world.SlotId);
            Assert.True(world.Slot(world.SlotId).IsBlocked);

            // What the button used to call: the block survives it.
            await world.Availability.ReleaseSlotAsync(world.SlotId);
            Assert.True(world.Slot(world.SlotId).IsBlocked);

            Assert.True(await world.Availability.UnblockSlotAsync(world.SlotId));
            Assert.False(world.Slot(world.SlotId).IsBlocked);

            Assert.False(await world.Availability.UnblockSlotAsync(Guid.NewGuid()));
        }

        // Opening a blocked hour must not hand it back to sale while a job still holds it.
        [Fact]
        public async Task OpeningABlockedHourLeavesItsBookingOnIt()
        {
            using var world = new BookingWorld();
            Booking booking = await world.SeedPaidBookingAsync();
            await world.Availability.BlockSlotAsync(world.SlotId);

            await world.Availability.UnblockSlotAsync(world.SlotId);

            Assert.True(world.Slot(world.SlotId).IsBooked);
            Assert.Equal(booking.Id, world.Slot(world.SlotId).BookingId);
        }

        // "Block Entire Day" marks the hours that jobs hold as well. "Release" on such an hour
        // freed it from its job and left the block, while the page said the hour was back on
        // sale (PROJECT_STATE.md Section 3ee).
        [Fact]
        public async Task ReleasingAnHourOnABlockedDayPutsItBackOnSale()
        {
            using var world = new BookingWorld();
            await world.SeedPaidBookingAsync();
            await world.Availability.BlockDateAsync(world.Slot(world.SlotId).StartTime);
            Assert.True(world.Slot(world.SlotId).IsBooked);
            Assert.True(world.Slot(world.SlotId).IsBlocked);

            Assert.True(await world.Availability.PutSlotBackOnSaleAsync(world.SlotId));

            AvailabilitySlot slot = world.Slot(world.SlotId);
            Assert.False(slot.IsBooked);
            Assert.Null(slot.BookingId);
            Assert.False(slot.IsBlocked);

            Assert.False(await world.Availability.PutSlotBackOnSaleAsync(Guid.NewGuid()));
        }

        // A job that is moved or cancelled on a blocked day leaves the block where it is:
        // nobody asked for that day to be opened.
        [Fact]
        public async Task AnHourFreedByItsJobOnABlockedDayStaysBlocked()
        {
            using var world = new BookingWorld();
            await world.SeedPaidBookingAsync();
            await world.Availability.BlockDateAsync(world.Slot(world.SlotId).StartTime);

            Assert.True(await world.Availability.ReleaseSlotAsync(world.SlotId));

            Assert.False(world.Slot(world.SlotId).IsBooked);
            Assert.True(world.Slot(world.SlotId).IsBlocked);
        }

        // What a job still owes, as its page says it (worked out in the page until PROJECT_STATE.md
        // Section 3dj). A job that was cancelled or abandoned owes nothing, whatever its estimate;
        // the £90 job here has its £50 deposit in.
        [Theory]
        [InlineData(BookingStatusNames.Approved, true, "£40.00", "£40.00")]
        [InlineData(BookingStatusNames.Completed, true, "£40.00", "£40.00")]
        [InlineData(BookingStatusNames.Cancelled, false, "Nothing: the job is cancelled", "Nothing")]
        [InlineData(BookingStatusNames.Abandoned, false, "Nothing: the job is abandoned", "Nothing")]
        public async Task AJobOwesWhatIsLeftUnlessItWasCalledOff(string status, bool owes, string line, string shortLine)
        {
            using var world = new BookingWorld();
            Booking booking = await world.SeedPaidBookingAsync();
            world.SetStatus(booking, status);

            var job = await world.Bookings.GetByIdAsync<BookingDetailsViewModel>(booking.Id);

            Assert.Equal(owes, job.OwesMoney);
            Assert.Equal(line, job.StillOwedText);
            Assert.Equal(shortLine, job.StillOwedShort);
        }

        // One still waiting for its deposit owes all of it; one with no price yet cannot say.
        [Fact]
        public async Task AJobWaitingForItsDepositOwesAllOfItAndOneWithNoPriceCannotSay()
        {
            using var world = new BookingWorld();
            await world.SeedPendingPaymentAsync("12 Main Rd, Sutton");
            var waiting = await world.Bookings.GetByIdAsync<BookingDetailsViewModel>(world.DbContext.Bookings.Single().Id);

            Assert.True(waiting.OwesMoney);
            Assert.Equal("£90.00", waiting.StillOwedText);

            using var other = new BookingWorld();
            Booking writtenIn = await other.SeedWrittenInJobAsync();
            var unpriced = await other.Bookings.GetByIdAsync<BookingDetailsViewModel>(writtenIn.Id);

            Assert.True(unpriced.OwesMoney);
            Assert.Equal("Not known until the final price is typed in", unpriced.StillOwedText);
            Assert.Equal("Not known", unpriced.StillOwedShort);
        }
    }
}
