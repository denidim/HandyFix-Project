namespace HandyFix.Data.Models
{
    using System;

    using System.Collections.Generic;

    using System.ComponentModel.DataAnnotations;

    using System.ComponentModel.DataAnnotations.Schema;

    using HandyFix.Data.Common.Models;

    public class Booking : BaseDeletableModel<Guid>
    {
        public Booking()
        {
            this.Id = Guid.NewGuid();
            this.BookingServices = new HashSet<BookingService>();
            this.Images = new HashSet<BookingImage>();
            this.Payments = new HashSet<Payment>();
            this.History = new HashSet<BookingHistoryEntry>();
        }

        public string UserId { get; set; }

        [ForeignKey(nameof(UserId))]
        public ApplicationUser User { get; set; }

        [Required(ErrorMessage = "The {0} field is required.")]
        [MinLength(2, ErrorMessage = "The {0} field must be at least 2 characters long.")]
        [MaxLength(100, ErrorMessage = "The {0} field cannot exceed 100 characters.")]
        public string CustomerFirstName { get; set; } = null!;

        // The four fields without [Required] below are ones the website's form always fills in
        // (BookingInputModel asks for them) and a job written in by the admin may leave empty:
        // a name and a phone number are enough for one (PROJECT_STATE.md Section 3ce).
        [MinLength(2, ErrorMessage = "The {0} field must be at least 2 characters long.")]
        [MaxLength(100, ErrorMessage = "The {0} field cannot exceed 100 characters.")]
        public string CustomerLastName { get; set; }

        [EmailAddress(ErrorMessage = "The {0} field is not a valid email address.")]
        [MaxLength(255, ErrorMessage = "The {0} field cannot exceed 255 characters.")]
        public string Email { get; set; }

        [Required(ErrorMessage = "The {0} field is required.")]
        [Phone(ErrorMessage = "The {0} field is not a valid phone number.")]
        [MaxLength(20, ErrorMessage = "The {0} field cannot exceed 20 characters.")]
        public string PhoneNumber { get; set; } = null!;

        [MinLength(10, ErrorMessage = "The {0} field must be at least 10 characters long.")]
        [MaxLength(300, ErrorMessage = "The {0} field cannot exceed 300 characters.")]
        public string Address { get; set; }

        [MinLength(10, ErrorMessage = "The {0} field must be at least 10 characters long.")]
        [MaxLength(3000, ErrorMessage = "The {0} field cannot exceed 3000 characters.")]
        public string ProblemDescription { get; set; }

        /// <summary>
        /// Where the job came from. Everything booked on the website is <see cref="BookingSource.Website"/>.
        /// </summary>
        public BookingSource Source { get; set; }

        /// <summary>
        /// The job's own day and hour. A website booking gets them from the slot it claims; a
        /// written-in job has them typed in and claims no slot. They are kept here, and not read
        /// off the slot, so that a cancelled or abandoned job still says when it was for: the slot
        /// it gives back no longer points at it.
        /// </summary>
        public DateTime? ScheduledStart { get; set; }

        public DateTime? ScheduledEnd { get; set; }

        [MaxLength(500, ErrorMessage = "The {0} field cannot exceed 500 characters.")]
        public string CancelReason { get; set; }

        /// <summary>
        /// What the job came to, typed in by the admin when it is marked done.
        /// <see cref="TotalAmount"/> is the estimate made when it was booked.
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        [Range(0.01, 100000, ErrorMessage = "The {0} must be between 0.01 and 100000.")]
        public decimal? FinalPrice { get; set; }

        /// <summary>
        /// Anything worth knowing next time. Only the admin sees it; it goes into no email and
        /// onto no page a customer can open.
        /// </summary>
        [MaxLength(4000, ErrorMessage = "The {0} field cannot exceed 4000 characters.")]
        public string AdminNotes { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Range(0.01, 100000, ErrorMessage = "The {0} must be between 0.01 and 100000.")]
        public decimal? TotalAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Range(0.01, 100000, ErrorMessage = "The {0} must be between 0.01 and 100000.")]
        public decimal? DepositAmount { get; set; }

        public virtual AvailabilitySlot AvailabilitySlot { get; set; }

        [Required(ErrorMessage = "The {0} field is required.")]
        public Guid StatusId { get; set; }

        public virtual BookingStatus Status { get; set; } = null!; // Pending (Чака одобрение), Approved (Одобрена), InProgress (Майсторът е на терен), Completed (Всичко е готово), Cancelled (Отказана).

        public Guid? TechnicianId { get; set; }

        public virtual Technician Technician { get; set; }

        public virtual ICollection<BookingService> BookingServices { get; set; }

        public virtual ICollection<BookingImage> Images { get; set; }

        public virtual ICollection<Payment> Payments { get; set; }

        public virtual ICollection<BookingHistoryEntry> History { get; set; }
    }
}
