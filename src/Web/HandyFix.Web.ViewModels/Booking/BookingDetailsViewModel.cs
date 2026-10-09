namespace HandyFix.Web.ViewModels.Booking
{
    using System;
    using System.Collections.Generic;
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

        // Which buttons the job's admin page offers. The service asks the same rules again
        // before it acts (BookingRules).
        public bool CanPickTechnician => BookingRules.CanPickTechnician(this.StatusName, this.CameFromWebsite, this.IsDepositPaid);

        public bool CanComplete => BookingRules.CanComplete(this.StatusName, this.CameFromWebsite, this.IsDepositPaid);

        public bool CanMove => BookingRules.CanMove(this.StatusName, this.CameFromWebsite, this.IsDepositPaid);

        public bool CanCancel => BookingRules.CanCancel(this.StatusName);

        public bool CanTakePayment => BookingRules.CanTakePayment(this.StatusName, this.CameFromWebsite, this.IsDepositPaid);

        public bool CanChangeFinalPrice => BookingRules.CanChangeFinalPrice(this.StatusName);

        public bool CanMarkDepositRefunded => BookingRules.CanMarkDepositRefunded(this.StatusName, this.IsDepositPaid || this.IsDepositRefunded);

        public bool CanEditDetails => BookingRules.CanEditDetails(this.StatusName);

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
                .Map(dest => dest.StatusName, src => src.Status != null ? src.Status.Name : "Pending")

                // The job's own date first. A booking saved before jobs kept one still has it on
                // its slot, for as long as it holds the slot.
                .Map(dest => dest.ScheduledTime, src => src.ScheduledStart != null ? src.ScheduledStart.Value : (src.AvailabilitySlot != null ? src.AvailabilitySlot.StartTime : default))
                .Map(dest => dest.ScheduledEndTime, src => src.ScheduledEnd != null ? src.ScheduledEnd.Value : (src.AvailabilitySlot != null ? src.AvailabilitySlot.EndTime : default))
                .Map(dest => dest.CreatedOn, src => src.CreatedOn)
                .Map(dest => dest.TechnicianName, src => src.Technician != null ? NameFormat.Full(src.Technician.FirstName, src.Technician.LastName) : "Not Assigned")
                .Map(dest => dest.TechnicianPhoneNumber, src => src.Technician != null ? src.Technician.PhoneNumber : null)
                .Map(dest => dest.IsDepositPaid, src => src.Payments != null && src.Payments.Any(p => p.Status.Name == "DepositPaid"))
                .Map(dest => dest.IsDepositRefunded, src => src.Payments != null && src.Payments.Any(p => p.Status.Name == "Refunded"))
                .Map(dest => dest.HasOtherPayments, src => src.Payments != null && src.Payments.Any(p => p.Status.Name == "Completed"))
                .Map(dest => dest.PaidRaw, src => src.Payments.Where(p => p.Status.Name == "DepositPaid" || p.Status.Name == "Completed").Sum(p => (double)p.Amount))
                .Map(dest => dest.Services, src => src.BookingServices != null ? src.BookingServices.Select(x => x.Service.Name) : new List<string>())
                .Map(dest => dest.ServiceId, src => src.BookingServices != null ? src.BookingServices.OrderBy(x => x.CreatedOn).Select(x => (Guid?)x.ServiceId).FirstOrDefault() : null)
                .Map(dest => dest.ImageUrls, src => src.Images != null ? src.Images.Select(x => x.ImageUrl) : new List<string>());
        }
    }
}
