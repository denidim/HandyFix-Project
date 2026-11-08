namespace HandyFix.Data.Models
{
    using System;

    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;

    using HandyFix.Data.Common.Models;

    public class Payment : BaseDeletableModel<Guid>
    {
        public Payment()
        {
            this.Id = Guid.NewGuid();
        }

        [Range(0.01, 100000, ErrorMessage = "The {0} must be between 0.01 and 100000.")]
        public decimal Amount { get; set; }

        /// <summary>
        /// A tip the customer added when paying the invoice online, for the technician. Kept
        /// apart from <see cref="Amount"/> so it never counts as money paid towards the job.
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        [Range(0, 10000, ErrorMessage = "The {0} must be between 0 and 10000.")]
        public decimal? Tip { get; set; }

        [Required(ErrorMessage = "The {0} field is required.")]
        [MaxLength(100, ErrorMessage = "The {0} field cannot exceed 100 characters.")]
        public string Provider { get; set; } = null!;   // e.g. "Stripe", "PayPal"

        /// <summary>
        /// How a payment the admin wrote on the job was made: "Card", "Cash" or "Bank transfer".
        /// "Card" too for an invoice paid online through Stripe. Empty for the deposit paid on
        /// the website, which is always a card through Stripe: that is how the two are told apart.
        /// </summary>
        [MaxLength(50, ErrorMessage = "The {0} field cannot exceed 50 characters.")]
        public string Method { get; set; }

        [MaxLength(500, ErrorMessage = "The {0} field cannot exceed 500 characters.")]
        public string TransactionId { get; set; }

        [MaxLength(500, ErrorMessage = "The {0} field cannot exceed 500 characters.")]
        public string CheckoutSessionId { get; set; }

        [Required(ErrorMessage = "The {0} field is required.")]
        public Guid StatusId { get; set; }

        public virtual PaymentStatus Status { get; set; } = null!;

        [Required(ErrorMessage = "The {0} field is required.")]
        public Guid BookingId { get; set; }

        public virtual Booking Booking { get; set; } = null!;
    }
}
