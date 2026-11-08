namespace HandyFix.Web.ViewModels.Booking
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;

    using HandyFix.Common;
    using HandyFix.Data.Models;
    using HandyFix.Services.Mapping;
    using HandyFix.Web.ViewModels.Administration.Technicians;

    using Mapster;

    using IMapFromBooking = HandyFix.Services.Mapping.IMapFrom<HandyFix.Data.Models.Booking>;

    public class BookingDetailsViewModel : IMapFromBooking, IHaveCustomMappings
    {
        public Guid Id { get; set; }

        public string CustomerFirstName { get; set; }

        public string CustomerLastName { get; set; }

        public string Email { get; set; }

        public string PhoneNumber { get; set; }

        public string Address { get; set; }

        public string ProblemDescription { get; set; }

        /// <summary>
        /// The estimate made when the job was booked: the service's price. Zero for a job
        /// written in with no service picked.
        /// </summary>
        public decimal TotalAmount { get; set; }

        public decimal DepositAmount { get; set; }

        /// <summary>
        /// What the job came to, typed in when it was marked done. Empty until then.
        /// </summary>
        public decimal? FinalPrice { get; set; }

        public string StatusName { get; set; }

        public BookingSource Source { get; set; }

        /// <summary>
        /// The job's own day and hour. Left at its default for a booking that was cancelled or
        /// abandoned before jobs kept their own date, which <see cref="HasDate"/> says.
        /// </summary>
        public DateTime ScheduledTime { get; set; }

        public DateTime ScheduledEndTime { get; set; }

        public DateTime CreatedOn { get; set; }

        public string TechnicianName { get; set; }

        public string TechnicianPhoneNumber { get; set; }

        public Guid? TechnicianId { get; set; }

        public string CancelReason { get; set; }

        public string AdminNotes { get; set; }

        /// <summary>
        /// The job's invoice number, once its invoice is made.
        /// </summary>
        public int? InvoiceNumber { get; set; }

        public DateTime? InvoicedOn { get; set; }

        public string InvoiceWorkDone { get; set; }

        public bool IsDepositPaid { get; set; }

        public bool IsDepositRefunded { get; set; }

        /// <summary>
        /// A payment the admin wrote on the job, beside or without the website deposit.
        /// </summary>
        public bool HasOtherPayments { get; set; }

        /// <summary>
        /// Everything that has come in and stayed, added up by the database. It is a double
        /// there because Sqlite, which the full-stack tests run on, cannot add up decimals;
        /// <see cref="Paid"/> is the figure to use.
        /// </summary>
        public double PaidRaw { get; set; }

        public decimal Paid => Math.Round((decimal)this.PaidRaw, 2);

        public bool HasDate => this.ScheduledTime != default;

        public bool CameFromWebsite => this.Source == BookingSource.Website;

        public string CustomerName => NameFormat.Full(this.CustomerFirstName, this.CustomerLastName);

        // The two labels every job carries (JobLabels): where the work stands, and the money.
        public string JobLabel => JobLabels.Job(this.StatusName);

        public string MoneyLabel => JobLabels.Money(this.Paid, this.FinalPrice, this.IsDepositPaid, this.HasOtherPayments, this.IsDepositRefunded);

        /// <summary>
        /// The final price once there is one, the estimate until then.
        /// </summary>
        public decimal Price => this.FinalPrice ?? this.TotalAmount;

        public decimal StillOwed => Math.Max(0m, this.Price - this.Paid);

        // What the job's page says about the job and its money. These were worked out in the
        // page itself until PROJECT_STATE.md Section 3dj.

        /// <summary>
        /// Whether anything can still be owed on the job. One that was cancelled or abandoned owes
        /// nothing, whatever its estimate was; one still waiting for its deposit owes all of it.
        /// </summary>
        public bool OwesMoney => this.JobLabel == JobLabels.WaitingForDeposit || this.JobLabel == JobLabels.Booked || this.JobLabel == JobLabels.Done;

        /// <summary>
        /// The "Still owed" line of the job's money list.
        /// </summary>
        public string StillOwedText => !this.OwesMoney ? "Nothing: the job is " + this.JobLabel.ToLowerInvariant()
            : this.Price > 0 ? Pounds(this.StillOwed)
            : "Not known until the final price is typed in";

        /// <summary>
        /// The same in the few words the summary card has room for.
        /// </summary>
        public string StillOwedShort => !this.OwesMoney ? "Nothing" : this.Price > 0 ? Pounds(this.StillOwed) : "Not known";

        /// <summary>
        /// Whether the customer was promised the time. Only a deposit promises it, and a deposit
        /// is only paid on the website.
        /// </summary>
        public string TimePromised => this.CameFromWebsite
            ? (this.IsDepositPaid ? "Yes, deposit paid" : this.IsDepositRefunded ? "No, deposit refunded" : "Not yet, deposit not paid")
            : "No, no deposit";

        /// <summary>
        /// Why the page offers no technician picker. A job still open has none only because its
        /// deposit is not in yet.
        /// </summary>
        public string TechnicianLockedReason => this.CanCancel
            ? "A technician can be picked once the deposit is paid."
            : "The technician can no longer be changed on this job.";

        // Which buttons the job's admin page offers. The service asks the same rules again
        // before it acts (BookingRules).
        public bool CanPickTechnician => BookingRules.CanPickTechnician(this.StatusName, this.CameFromWebsite, this.IsDepositPaid);

        public bool CanComplete => BookingRules.CanComplete(this.StatusName, this.CameFromWebsite, this.IsDepositPaid);

        public bool CanMove => BookingRules.CanMove(this.StatusName, this.CameFromWebsite, this.IsDepositPaid);

        public bool CanCancel => BookingRules.CanCancel(this.StatusName);

        public bool CanTakePayment => BookingRules.CanTakePayment(this.StatusName, this.CameFromWebsite, this.IsDepositPaid, this.InvoiceNumber.HasValue);

        public bool PaymentWaitsForInvoice => BookingRules.PaymentWaitsForInvoice(this.StatusName, this.InvoiceNumber.HasValue);

        /// <summary>
        /// What the invoice's "work done" box opens with: the text kept on the job, or the job's
        /// services before any is kept, for the admin to add the hours and materials to.
        /// </summary>
        public string InvoiceWorkDoneOrServices => string.IsNullOrWhiteSpace(this.InvoiceWorkDone)
            ? string.Join(", ", this.Services ?? Enumerable.Empty<string>())
            : this.InvoiceWorkDone;

        public bool CanChangeFinalPrice => BookingRules.CanChangeFinalPrice(this.StatusName);

        public bool CanMarkDepositRefunded => BookingRules.CanMarkDepositRefunded(this.StatusName, this.IsDepositPaid || this.IsDepositRefunded);

        public bool CanEditDetails => BookingRules.CanEditDetails(this.StatusName);

        public bool CanInvoice => BookingRules.CanInvoice(this.StatusName);

        /// <summary>
        /// "Invoice PHS-0001, made 7 Nov 2026", or that none is made yet.
        /// </summary>
        public string InvoiceLine => this.InvoiceNumber.HasValue && this.InvoicedOn.HasValue
            ? $"Invoice {InvoiceTerms.Number(this.InvoiceNumber.Value)}, made {UkTime.FromUtc(this.InvoicedOn.Value).ToString("d MMM yyyy", CultureInfo.InvariantCulture)}."
            : "No invoice made yet. The first download or email gives it its number.";

        // The two the customer's own pages ask: may the deposit still be paid, and is there a
        // confirmed booking to show.
        public bool CanPayDeposit => BookingRules.CanPayDeposit(this.StatusName, this.CameFromWebsite, this.IsDepositPaid);

        public bool IsConfirmed => BookingRules.IsConfirmed(this.StatusName, this.CameFromWebsite, this.IsDepositPaid);

        public IEnumerable<string> Services { get; set; }

        /// <summary>
        /// The service the job has, by its id, for the form that edits the job's details. It is
        /// read off the job's own line, not off the service: a job booked with a service that
        /// was deleted since still has its id here, though <see cref="Services"/> no longer
        /// lists its name.
        /// </summary>
        public Guid? ServiceId { get; set; }

        public IEnumerable<string> ImageUrls { get; set; }

        /// <summary>
        /// Populated by the controller, not mapped: the assignment picker's options.
        /// </summary>
        public IEnumerable<TechnicianOptionViewModel> Technicians { get; set; } = new List<TechnicianOptionViewModel>();

        /// <summary>
        /// Populated by the controller for the job's page, not mapped: the money list.
        /// </summary>
        public IEnumerable<PaymentLineViewModel> Payments { get; set; } = new List<PaymentLineViewModel>();

        /// <summary>
        /// Populated by the controller for the job's page, not mapped: what happened, oldest first.
        /// </summary>
        public IEnumerable<BookingHistoryViewModel> History { get; set; } = new List<BookingHistoryViewModel>();

        public void CreateMappings(TypeAdapterConfig config)
        {
            config.NewConfig<Booking, BookingDetailsViewModel>()
                .Ignore(dest => dest.Technicians)
                .Ignore(dest => dest.Payments)
                .Ignore(dest => dest.History)
                .Map(dest => dest.TotalAmount, src => src.TotalAmount ?? 0m)
                .Map(dest => dest.DepositAmount, src => src.DepositAmount ?? 0m)
                .Map(dest => dest.StatusName, src => src.Status != null ? src.Status.Name : BookingStatusNames.Pending)

                // The job's own date first. A booking saved before jobs kept one still has it on
                // its slot, for as long as it holds the slot.
                .Map(dest => dest.ScheduledTime, src => src.ScheduledStart != null ? src.ScheduledStart.Value : (src.AvailabilitySlot != null ? src.AvailabilitySlot.StartTime : default))
                .Map(dest => dest.ScheduledEndTime, src => src.ScheduledEnd != null ? src.ScheduledEnd.Value : (src.AvailabilitySlot != null ? src.AvailabilitySlot.EndTime : default))
                .Map(dest => dest.CreatedOn, src => src.CreatedOn)
                .Map(dest => dest.TechnicianName, src => src.Technician != null ? NameFormat.Full(src.Technician.FirstName, src.Technician.LastName) : "Not Assigned")
                .Map(dest => dest.TechnicianPhoneNumber, src => src.Technician != null ? src.Technician.PhoneNumber : null)

                // No "!= null" on a job's lists below. This runs as SQL, where a list is never
                // null, and EF fetched the whole list to check: the job list's query joined the
                // payments three times more, the services twice and the photos once, and SQL
                // Server took about three seconds to plan it (PROJECT_STATE.md Section 3cs).
                .Map(dest => dest.IsDepositPaid, src => src.Payments.Any(p => p.Status.Name == PaymentStatusNames.DepositPaid))
                .Map(dest => dest.IsDepositRefunded, src => src.Payments.Any(p => p.Status.Name == PaymentStatusNames.Refunded))
                .Map(dest => dest.HasOtherPayments, src => src.Payments.Any(p => p.Status.Name == PaymentStatusNames.Completed))
                .Map(dest => dest.PaidRaw, src => src.Payments.Where(p => p.Status.Name == PaymentStatusNames.DepositPaid || p.Status.Name == PaymentStatusNames.Completed).Sum(p => (double)p.Amount))
                .Map(dest => dest.Services, src => src.BookingServices.Select(x => x.Service.Name))
                .Map(dest => dest.ServiceId, src => src.BookingServices.OrderBy(x => x.CreatedOn).Select(x => (Guid?)x.ServiceId).FirstOrDefault())
                .Map(dest => dest.ImageUrls, src => src.Images.Select(x => x.ImageUrl));
        }

        // An amount as the job's page writes one: a dot before the pence, whatever language the
        // server is set to.
        private static string Pounds(decimal amount) => "£" + amount.ToString("0.00", CultureInfo.InvariantCulture);
    }
}
