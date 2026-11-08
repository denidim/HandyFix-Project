namespace HandyFix.Services.Data.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    using HandyFix.Common;
    using HandyFix.Data.Models;
    using HandyFix.Services.Data.Invoices;
    using HandyFix.Web.ViewModels.Administration.Invoices;

    using Xunit;

    // A job's invoice: one number per job, the deposit and every later payment taken off the
    // final price, the email with the PDF, and the month's list for the accountant.
    public class InvoicesServiceTests
    {
        [Fact]
        public async Task ADoneWebsiteBookingGetsInvoiceOneWithTheDepositTakenOff()
        {
            using var world = new BookingWorld();
            Booking booking = await DoneBookingAsync(world, 160m);

            InvoiceViewModel invoice = await world.Invoices.MakeAsync(booking.Id, "  Replaced the kitchen tap.  ");

            Assert.Equal(1, invoice.Number);
            Assert.Equal("PHS-0001", invoice.NumberText);
            Assert.Equal(160m, invoice.Total);
            Assert.Equal(50m, invoice.Paid);
            Assert.Equal(50m, invoice.PaidDeposit);
            Assert.Equal(110m, invoice.Balance);
            Assert.False(invoice.IsPaid);
            Assert.Equal("Booking deposit, paid online by card", Assert.Single(invoice.Payments).Description);
            Assert.Equal("Replaced the kitchen tap.", invoice.WorkDone);
            Assert.Equal("Leak Fix", invoice.Services);
            Assert.Equal("Ada Lovelace", invoice.CustomerName);
            Assert.Equal(invoice.InvoicedOn.Date, invoice.DueOn);
            Assert.Contains("Invoice PHS-0001 made.", world.HistoryOf(booking));
        }

        [Fact]
        public async Task MakingItAgainKeepsTheNumberAndTheDateAndTakesTheNewText()
        {
            using var world = new BookingWorld();
            Booking first = await DoneBookingAsync(world, 160m);
            Booking second = await world.SeedWrittenInJobAsync();
            await world.Jobs.CompleteBookingAsync(second.Id, 90m);

            InvoiceViewModel made = await world.Invoices.MakeAsync(first.Id, "First text");
            InvoiceViewModel other = await world.Invoices.MakeAsync(second.Id, null);
            InvoiceViewModel again = await world.Invoices.MakeAsync(first.Id, "Second text");

            Assert.Equal(1, again.Number);
            Assert.Equal(2, other.Number);
            Assert.Equal(made.InvoicedOn, again.InvoicedOn);
            Assert.Equal("Second text", again.WorkDone);
            Assert.Single(world.HistoryOf(first), line => line.StartsWith("Invoice", StringComparison.Ordinal));
        }

        [Fact]
        public async Task APaymentWrittenLaterShowsTheInvoiceAsPaid()
        {
            using var world = new BookingWorld();
            Booking booking = await DoneBookingAsync(world, 160m);
            await world.Invoices.MakeAsync(booking.Id, null);

            await world.Payments.AddPaymentAsync(booking.Id, 110m, PaymentMethods.Cash);
            InvoiceViewModel invoice = await world.Invoices.GetAsync(booking.Id);

            Assert.True(invoice.IsPaid);
            Assert.Equal(0m, invoice.Balance);
            Assert.Equal(110m, invoice.PaidInCash);
            Assert.Equal("Paid", invoice.StatusOn(invoice.DueOn.AddDays(10)));
            Assert.Equal(new[] { "Booking deposit, paid online by card", "Paid in cash" }, invoice.Payments.Select(x => x.Description));
        }

        [Fact]
        public async Task AJobThatIsNotDoneGetsNoInvoice()
        {
            using var world = new BookingWorld();
            Booking booking = await world.SeedPaidBookingAsync();

            Assert.Null(await world.Invoices.MakeAsync(booking.Id, "Too early"));
            Assert.Equal(InvoiceEmailOutcome.NotAllowed, (await world.Invoices.EmailAsync(booking.Id, null)).Outcome);
            Assert.Null(await world.Invoices.GetAsync(booking.Id));
            Assert.Null(world.DbContext.Bookings.Single().InvoiceNumber);
        }

        [Fact]
        public async Task EmailingSendsThePdfToTheCustomerAndSaysSoOnTheJob()
        {
            using var world = new BookingWorld();
            Booking booking = await DoneBookingAsync(world, 160m);
            List<SentEmail> sent = world.CaptureEmails();

            InvoiceEmailResult result = await world.Invoices.EmailAsync(booking.Id, "Replaced the kitchen tap.");

            Assert.Equal(InvoiceEmailOutcome.Sent, result.Outcome);
            SentEmail email = Assert.Single(sent);
            Assert.Equal("ada@example.com", email.To);
            Assert.Contains("PHS-0001", email.Subject);
            Assert.Contains("110.00", email.WhatIsRead());
            Assert.Contains($"/Payment/Invoice/{booking.Id}", email.Body);
            Assert.Contains("Pay &pound;110.00 Online (Card, Apple Pay, Google Pay)", email.WhatIsRead());
            Assert.Contains(InvoiceOptions.DefaultPaymentDetails, email.WhatIsRead());
            var pdf = Assert.Single(email.Attachments);
            Assert.Equal("Invoice PHS-0001.pdf", pdf.FileName);
            Assert.StartsWith("%PDF", Encoding.ASCII.GetString(pdf.Content, 0, 4));
            Assert.Contains(world.HistoryOf(booking), line => line.StartsWith("Invoice PHS-0001 emailed to the customer: £110.00 to pay by", StringComparison.Ordinal));
        }

        [Fact]
        public async Task AJobWithNoEmailAddressStillGetsItsInvoice()
        {
            using var world = new BookingWorld();
            Booking job = await world.SeedWrittenInJobAsync();
            await world.Jobs.CompleteBookingAsync(job.Id, 90m);
            List<SentEmail> sent = world.CaptureEmails();

            InvoiceEmailResult result = await world.Invoices.EmailAsync(job.Id, null);

            Assert.Equal(InvoiceEmailOutcome.NoEmailAddress, result.Outcome);
            Assert.Equal(1, result.Invoice.Number);
            Assert.Equal(90m, result.Invoice.Balance);
            Assert.Empty(sent);
        }

        [Fact]
        public async Task AnEmailThatFailsIsWrittenOnTheJob()
        {
            using var world = new BookingWorld();
            Booking booking = await DoneBookingAsync(world, 160m);
            world.EmailsFail();

            InvoiceEmailResult result = await world.Invoices.EmailAsync(booking.Id, null);

            Assert.Equal(InvoiceEmailOutcome.NotSent, result.Outcome);
            Assert.Contains("Invoice PHS-0001 could not be emailed to the customer.", world.HistoryOf(booking));
        }

        [Fact]
        public async Task TheMonthsListHoldsOnlyTheInvoicesMadeInIt()
        {
            using var world = new BookingWorld();
            Booking thisMonth = await DoneBookingAsync(world, 160m);
            Booking lastMonth = await world.SeedWrittenInJobAsync();
            await world.Jobs.CompleteBookingAsync(lastMonth.Id, 90m);
            await world.Invoices.MakeAsync(thisMonth.Id, null);
            await world.Invoices.MakeAsync(lastMonth.Id, null);

            Booking moved = world.DbContext.Bookings.Single(x => x.Id == lastMonth.Id);
            moved.InvoicedOn = moved.InvoicedOn.Value.AddMonths(-1);
            world.DbContext.SaveChanges();

            DateTime today = UkTime.FromUtc(DateTime.UtcNow);
            IReadOnlyList<InvoiceViewModel> invoices = await world.Invoices.GetMonthAsync(today.Year, today.Month);

            Assert.Equal(thisMonth.Id, Assert.Single(invoices).BookingId);
        }

        [Fact]
        public void TheCsvSaysHowEachPoundCameInAndKeepsANameAsText()
        {
            var invoice = new InvoiceViewModel
            {
                Number = 7,
                InvoicedOn = new DateTime(2026, 11, 7),
                CustomerName = "=Smith, \"Jo\"",
                JobReference = "AB12CD34",
                Services = "Leak Fix",
                Total = 175m,
                Payments = new List<InvoicePaymentLine>
                {
                    new InvoicePaymentLine { Method = InvoicePaymentLine.DepositMethod, Amount = 50m },
                    new InvoicePaymentLine { Method = PaymentMethods.BankTransfer, Amount = 25m },
                },
            };

            var lines = Encoding.UTF8.GetString(InvoiceCsv.For(new[] { invoice }, new DateTime(2026, 11, 7))).TrimStart((char)0xFEFF).Split(Environment.NewLine);

            Assert.StartsWith("Invoice,Date,Customer,Job,Services,Total,", lines[0]);
            Assert.Equal("PHS-0007,2026-11-07,\"'=Smith, \"\"Jo\"\"\",AB12CD34,Leak Fix,175.00,50.00,0.00,0.00,25.00,75.00,100.00,Due 7 Nov", lines[1]);
        }

        [Fact]
        public void AMonthWithNoInvoicesStillMakesAListAndThePdfsAreReal()
        {
            using var world = new BookingWorld();
            var invoice = new InvoiceViewModel { Number = 1, InvoicedOn = new DateTime(2026, 11, 7), CustomerName = "Ada Lovelace", JobReference = "AB12CD34", Total = 90m };

            Assert.StartsWith("%PDF", Encoding.ASCII.GetString(world.Invoices.MonthPdf(Array.Empty<InvoiceViewModel>(), 2026, 11), 0, 4));
            Assert.StartsWith("%PDF", Encoding.ASCII.GetString(world.Invoices.MonthPdf(new[] { invoice }, 2026, 11), 0, 4));
            Assert.StartsWith("%PDF", Encoding.ASCII.GetString(world.Invoices.Pdf(invoice), 0, 4));
        }

        [Fact]
        public void AnInvoiceNotPaidByItsDayIsOverdue()
        {
            var invoice = new InvoiceViewModel { Number = 1, InvoicedOn = new DateTime(2026, 11, 7, 18, 30, 0), Total = 90m };

            Assert.Equal("Due 7 Nov", invoice.StatusOn(new DateTime(2026, 11, 7)));
            Assert.Equal("Overdue", invoice.StatusOn(new DateTime(2026, 11, 8)));
        }

        // A website booking with its £50 deposit paid, marked done at the price given.
        private static async Task<Booking> DoneBookingAsync(BookingWorld world, decimal finalPrice)
        {
            Booking booking = await world.SeedPaidBookingAsync();
            Assert.True(await world.Jobs.CompleteBookingAsync(booking.Id, finalPrice));
            return booking;
        }
    }
}
