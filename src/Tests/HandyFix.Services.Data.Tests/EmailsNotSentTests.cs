namespace HandyFix.Services.Data.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Data.Models;
    using HandyFix.Data.Repositories;
    using HandyFix.Services.Data.EmailsNotSent;
    using HandyFix.Web.ViewModels.Administration.Dashboard;

    using Xunit;

    // An email the site could not send is kept where the admin will see it: a line in the job's
    // history and a row the dashboard lists (PROJECT_STATE.md Section 3cq). Until then a failed
    // send was a line in the server's log and nothing else, so nobody knew to tell a customer
    // another way, or that a deposit had come in. The enquiry's own emails are in
    // InquiriesServiceTests.
    public class EmailsNotSentTests
    {
        private const string CustomerLine = "The email confirming the deposit could not be sent to the customer. Tell them another way that the booking is confirmed.";
        private const string CompanyLine = "The email telling the company of this deposit could not be sent.";

        [Fact]
        public async Task ADepositWhoseTwoEmailsFailIsMarkedOnTheJobForEach()
        {
            using var world = new BookingWorld();
            world.EmailsFail();

            Booking booking = await world.SeedPaidBookingAsync();

            List<string> history = world.HistoryOf(booking);
            Assert.Contains(CustomerLine, history);
            Assert.Contains(CompanyLine, history);
            Assert.Equal(
                new[] { "The deposit confirmation, to the customer", "The notice of a paid deposit, to the company" },
                WhatWasNotSent(world, booking));
        }

        [Fact]
        public async Task OnlyTheEmailThatFailedIsMarked()
        {
            using var world = new BookingWorld();
            world.EmailsFail("ada@example.com");

            Booking booking = await world.SeedPaidBookingAsync();

            Assert.Contains(CustomerLine, world.HistoryOf(booking));
            Assert.DoesNotContain(CompanyLine, world.HistoryOf(booking));
            Assert.Equal(new[] { "The deposit confirmation, to the customer" }, WhatWasNotSent(world, booking));
        }

        [Fact]
        public async Task ADepositWhoseEmailsGoOutLeavesNoMark()
        {
            using var world = new BookingWorld();
            world.CaptureEmails();

            Booking booking = await world.SeedPaidBookingAsync();

            Assert.DoesNotContain(world.HistoryOf(booking), line => line.Contains("could not be sent"));
            Assert.Empty(world.DbContext.EmailsNotSent);
        }

        // Money for a booking that was dropped: the company's notice is the only email, and the
        // only way anyone hears of it.
        [Fact]
        public async Task MoneyForABookingNotHeldWhoseNoticeFailsIsMarked()
        {
            using var world = new BookingWorld();
            var checkoutSessionId = await world.SeedPendingPaymentAsync("12 Main Rd, Sutton");
            Booking booking = world.DbContext.Bookings.Single();
            world.SetStatus(booking, "Abandoned");
            world.EmailsFail();

            await world.Payments.ProcessPaymentSuccessAsync(checkoutSessionId, "txn_late");

            Assert.Contains(CompanyLine, world.HistoryOf(booking));
            Assert.Equal(new[] { "The notice of a deposit for a booking not held, to the company" }, WhatWasNotSent(world, booking));
        }

        // The technician's email already wrote its own history line (Section 3ce); it is on the
        // dashboard's list now too.
        [Fact]
        public async Task ATechnicianEmailThatFailsIsListedForTheDashboard()
        {
            using var world = new BookingWorld();
            world.CaptureEmails();
            Booking booking = await world.SeedPaidBookingAsync();
            Technician technician = world.AddTechnician("Zapryan");
            world.EmailsFail();

            await world.Bookings.AssignTechnicianAsync(booking.Id, technician.Id);

            Assert.Contains("Technician picked: Zapryan. The email to the customer could not be sent.", world.HistoryOf(booking));
            Assert.Equal(new[] { "The technician's name, to the customer" }, WhatWasNotSent(world, booking));
        }

        // The dashboard lists the last week's, the newest first, each with the job or the enquiry
        // it was about and the name to show for it.
        [Fact]
        public async Task TheDashboardReadsTheLastWeekNewestFirstWithWhoEachWasAbout()
        {
            using var world = new BookingWorld();
            world.EmailsFail();
            Booking booking = await world.SeedPaidBookingAsync();
            var inquiry = new Inquiry { Name = "Jane Doe", Email = "jane@example.com", PhoneNumber = "07700 900123", Message = "Kitchen tap is dripping." };
            world.DbContext.Inquiries.Add(inquiry);
            world.DbContext.EmailsNotSent.Add(new EmailNotSent { What = "The acknowledgement, to the sender", InquiryId = inquiry.Id, CreatedOn = DateTime.UtcNow.AddMinutes(5) });
            world.DbContext.EmailsNotSent.Add(new EmailNotSent { What = "An old one", BookingId = booking.Id, CreatedOn = DateTime.UtcNow.AddDays(-8) });
            await world.DbContext.SaveChangesAsync();

            var service = new EmailsNotSentService(new EfRepository<EmailNotSent>(world.DbContext));
            List<EmailNotSentViewModel> listed = (await service.GetSinceAsync<EmailNotSentViewModel>(DateTime.UtcNow.AddDays(-7))).ToList();

            Assert.Equal(3, listed.Count);
            Assert.Equal("The acknowledgement, to the sender", listed[0].What);
            Assert.Equal(inquiry.Id, listed[0].InquiryId);
            Assert.Equal("Jane Doe", listed[0].InquiryName);
            Assert.Null(listed[0].BookingId);
            Assert.All(listed.Skip(1), email =>
            {
                Assert.Equal(booking.Id, email.BookingId);
                Assert.Equal("Ada", email.CustomerFirstName);
                Assert.Equal("Lovelace", email.CustomerLastName);
            });
            Assert.DoesNotContain(listed, email => email.What == "An old one");
        }

        private static string[] WhatWasNotSent(BookingWorld world, Booking booking) =>
            world.DbContext.EmailsNotSent
                .Where(x => x.BookingId == booking.Id)
                .Select(x => x.What)
                .OrderBy(x => x)
                .ToArray();
    }
}
