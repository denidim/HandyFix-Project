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
        }

        public string UserId { get; set; }

        [ForeignKey(nameof(UserId))]
        public ApplicationUser User { get; set; }

        [Required(ErrorMessage = "The {0} field is required.")]
        [MinLength(2, ErrorMessage = "The {0} field must be at least 2 characters long.")]
        [MaxLength(100, ErrorMessage = "The {0} field cannot exceed 100 characters.")]
        public string CustomerFirstName { get; set; } = null!;

        [Required(ErrorMessage = "The {0} field is required.")]
        [MinLength(2, ErrorMessage = "The {0} field must be at least 2 characters long.")]
        [MaxLength(100, ErrorMessage = "The {0} field cannot exceed 100 characters.")]
        public string CustomerLastName { get; set; } = null!;

        [Required(ErrorMessage = "The {0} field is required.")]
        [EmailAddress(ErrorMessage = "The {0} field is not a valid email address.")]
        [MaxLength(255, ErrorMessage = "The {0} field cannot exceed 255 characters.")]
        public string Email { get; set; } = null!;

        [Required(ErrorMessage = "The {0} field is required.")]
        [Phone(ErrorMessage = "The {0} field is not a valid phone number.")]
        [MaxLength(20, ErrorMessage = "The {0} field cannot exceed 20 characters.")]
        public string PhoneNumber { get; set; } = null!;

        [Required(ErrorMessage = "The {0} field is required.")]
        [MinLength(10, ErrorMessage = "The {0} field must be at least 10 characters long.")]
        [MaxLength(300, ErrorMessage = "The {0} field cannot exceed 300 characters.")]
        public string Address { get; set; } = null!;

        [Required(ErrorMessage = "The {0} field is required.")]
        [MinLength(10, ErrorMessage = "The {0} field must be at least 10 characters long.")]
        [MaxLength(3000, ErrorMessage = "The {0} field cannot exceed 3000 characters.")]
        public string ProblemDescription { get; set; } = null!;

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
    }
}
