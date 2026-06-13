namespace HandyFix.Data.Models
{
    using System;

    using System.ComponentModel.DataAnnotations;

    using System.ComponentModel.DataAnnotations.Schema;

    using HandyFix.Data.Common.Models;

    public class BookingService : BaseDeletableModel<Guid>
    {
        public BookingService()
        {
            this.Id = Guid.NewGuid();
        }

        [Required]
        public Guid BookingId { get; set; }

        public virtual Booking Booking { get; set; } = null!;

        [Required]
        public Guid ServiceId { get; set; }

        public virtual Service Service { get; set; } = null!;

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        [Range(0.01, 100000, ErrorMessage = "The {0} must be between 0.01 and 100000.")]
        public decimal PriceAtBooking { get; set; }

        public int Quantity { get; set; } = 1;

        public string Notes { get; set; }
    }
}
