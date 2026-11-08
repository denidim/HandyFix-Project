namespace HandyFix.Services.Data.Invoices
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Common;
    using HandyFix.Data.Common.Repositories;
    using HandyFix.Data.Models;
    using HandyFix.Services.Data.Common;
    using HandyFix.Services.Data.Emails;
    using HandyFix.Web.ViewModels.Administration.Invoices;

    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Options;

    // The invoice is not a record of its own: a job carries its number, its date and the "work
    // done" text, and everything else is read off the job each time. The business is not
    // registered for VAT, so no law asks for a second invoice when the deposit is paid; the
    // deposit shows on this one as paid (decided with the developer on 2026-11-07).
    public class InvoicesService : IInvoicesService
    {
        // As long as Booking.InvoiceWorkDone can hold.
        private const int MaxWorkDoneLength = 2000;

        // Two invoices made in the same moment would be given the same number; the unique index
        // refuses the second, which then takes the next one.
        private const int NumberAttempts = 3;

        private readonly IDeletableEntityRepository<Booking> bookingRepository;
        private readonly IBookingEmails bookingEmails;
        private readonly InvoiceOptions settings;
        private readonly TimeProvider clock;

        public InvoicesService(
            IDeletableEntityRepository<Booking> bookingRepository,
            IBookingEmails bookingEmails,
            IOptions<InvoiceOptions> settings,
            TimeProvider clock)
        {
            this.bookingRepository = bookingRepository;
            this.bookingEmails = bookingEmails;
            this.settings = settings.Value;
            this.clock = clock;
        }

        public async Task<InvoiceViewModel> MakeAsync(Guid bookingId, string workDone)
        {
            Booking booking = await this.LoadAsync(bookingId);
            return await this.MakeAsync(booking, workDone) ? ToInvoice(booking) : null;
        }

        public async Task<InvoiceViewModel> GetAsync(Guid bookingId)
        {
            Booking booking = await this.LoadAsync(bookingId);
            return booking?.InvoiceNumber != null ? ToInvoice(booking) : null;
        }

        public async Task<InvoiceEmailResult> EmailAsync(Guid bookingId, string workDone)
        {
            Booking booking = await this.LoadAsync(bookingId);
            if (!await this.MakeAsync(booking, workDone))
            {
                return new InvoiceEmailResult { Outcome = InvoiceEmailOutcome.NotAllowed };
            }

            InvoiceViewModel invoice = ToInvoice(booking);
            if (string.IsNullOrWhiteSpace(booking.Email))
            {
                return new InvoiceEmailResult { Outcome = InvoiceEmailOutcome.NoEmailAddress, Invoice = invoice };
            }

            var sent = await this.bookingEmails.InvoiceAsync(booking.CustomerFirstName, invoice, this.Pdf(invoice), this.settings.PaymentDetails);

            booking.History.Add(JobHistory.Line(sent
                ? $"Invoice {invoice.NumberText} emailed to the customer: " + (invoice.IsPaid
                    ? "paid in full."
                    : $"{JobHistory.Pounds(invoice.Balance)} to pay by {invoice.DueOn.ToString("d MMM yyyy", CultureInfo.InvariantCulture)}.")
                : $"Invoice {invoice.NumberText} could not be emailed to the customer."));
            await this.bookingRepository.SaveChangesAsync();

            return new InvoiceEmailResult { Outcome = sent ? InvoiceEmailOutcome.Sent : InvoiceEmailOutcome.NotSent, Invoice = invoice };
        }

        public async Task<IReadOnlyList<InvoiceViewModel>> GetMonthAsync(int year, int month)
        {
            var firstDay = new DateTime(year, month, 1);
            DateTime fromUtc = UkTime.ToUtc(firstDay);
            DateTime toUtc = UkTime.ToUtc(firstDay.AddMonths(1));

            List<Booking> bookings = await WithInvoiceData(this.bookingRepository.AllAsNoTracking())
                .Where(x => x.InvoiceNumber != null && x.InvoicedOn >= fromUtc && x.InvoicedOn < toUtc)
                .OrderBy(x => x.InvoiceNumber)
                .ToListAsync();

            return bookings.Select(ToInvoice).ToList();
        }

        public byte[] Pdf(InvoiceViewModel invoice)
        {
            return InvoicePdf.ForInvoice(invoice, this.settings.PaymentDetails, this.settings.PaymentUrl(invoice.BookingId));
        }

        public byte[] MonthPdf(IReadOnlyList<InvoiceViewModel> invoices, int year, int month)
        {
            return InvoicePdf.ForMonth(invoices, year, month, this.TodayUk());
        }

        public byte[] MonthCsv(IReadOnlyList<InvoiceViewModel> invoices)
        {
            return InvoiceCsv.For(invoices, this.TodayUk());
        }

        // What an invoice is read from: the job, its payments with their kinds, its services.
        private static IQueryable<Booking> WithInvoiceData(IQueryable<Booking> bookings)
        {
            return bookings
                .Include(x => x.Status)
                .Include(x => x.Payments).ThenInclude(p => p.Status)
                .Include(x => x.BookingServices).ThenInclude(s => s.Service);
        }

        private static InvoiceViewModel ToInvoice(Booking booking)
        {
            return new InvoiceViewModel
            {
                BookingId = booking.Id,
                Number = booking.InvoiceNumber.Value,
                InvoicedOn = UkTime.FromUtc(booking.InvoicedOn.Value),
                CustomerName = NameFormat.Full(booking.CustomerFirstName, booking.CustomerLastName),
                Address = booking.Address,
                Email = booking.Email,
                PhoneNumber = booking.PhoneNumber,
                JobReference = BookingReference.Short(booking.Id),

                // A service deleted since the job was booked is left out by the query filter.
                Services = string.Join(", ", booking.BookingServices
                    .Select(x => x.Service?.Name)
                    .Where(name => !string.IsNullOrWhiteSpace(name))),
                WorkDone = booking.InvoiceWorkDone,
                WorkDate = booking.ScheduledStart,
                Total = booking.FinalPrice ?? 0m,

                // Money that came in and stayed: the website deposit unless it went back, and
                // every payment the admin wrote on the job.
                Payments = booking.Payments
                    .Where(p => p.Status?.Name == PaymentStatusNames.DepositPaid || p.Status?.Name == PaymentStatusNames.Completed)
                    .OrderBy(p => p.CreatedOn)
                    .Select(p => new InvoicePaymentLine
                    {
                        PaidOn = UkTime.FromUtc(p.CreatedOn),
                        Method = p.Status.Name == PaymentStatusNames.DepositPaid ? InvoicePaymentLine.DepositMethod : p.Method,
                        Amount = p.Amount,
                        Tip = p.Tip ?? 0m,
                    })
                    .ToList(),
            };
        }

        private static string Tidy(string workDone)
        {
            if (string.IsNullOrWhiteSpace(workDone))
            {
                return null;
            }

            var text = workDone.Trim();
            return text.Length > MaxWorkDoneLength ? text.Substring(0, MaxWorkDoneLength) : text;
        }

        private Task<Booking> LoadAsync(Guid bookingId)
        {
            return WithInvoiceData(this.bookingRepository.All()).FirstOrDefaultAsync(x => x.Id == bookingId);
        }

        // Keeps the text and, the first time, gives the job its number and its invoice date.
        // False when the job is missing or not done: only done work has a final price.
        private async Task<bool> MakeAsync(Booking booking, string workDone)
        {
            if (booking == null || booking.FinalPrice == null || !BookingRules.CanInvoice(booking.Status?.Name))
            {
                return false;
            }

            booking.InvoiceWorkDone = Tidy(workDone);

            if (booking.InvoiceNumber != null)
            {
                await this.bookingRepository.SaveChangesAsync();
                return true;
            }

            booking.InvoicedOn = this.clock.GetUtcNow().UtcDateTime;
            BookingHistoryEntry line = JobHistory.Line(string.Empty);
            booking.History.Add(line);

            for (var attempt = 1; ; attempt++)
            {
                booking.InvoiceNumber = await this.NextNumberAsync();
                line.Text = $"Invoice {InvoiceTerms.Number(booking.InvoiceNumber.Value)} made.";

                try
                {
                    await this.bookingRepository.SaveChangesAsync();
                    return true;
                }
                catch (DbUpdateException) when (attempt < NumberAttempts)
                {
                    // Taken in the same moment by another invoice: try the next number.
                }
            }
        }

        // The next number after the highest ever given, deleted jobs included, so no number is
        // given twice.
        private async Task<int> NextNumberAsync()
        {
            int? highest = await this.bookingRepository.AllAsNoTrackingWithDeleted().MaxAsync(x => x.InvoiceNumber);
            return (highest ?? 0) + 1;
        }

        private DateTime TodayUk()
        {
            return UkTime.FromUtc(this.clock.GetUtcNow().UtcDateTime).Date;
        }
    }
}
