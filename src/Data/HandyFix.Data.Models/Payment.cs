namespace HandyFix.Data.Models
{
    using System;

    using System.ComponentModel.DataAnnotations;

    using HandyFix.Data.Common.Models;

    public class Payment : BaseDeletableModel<Guid>
    {
        public Payment()
        {
            this.Id = Guid.NewGuid();
        }

        [Range(0.01, 100000, ErrorMessage = "The {0} must be between 0.01 and 100000.")]
        public decimal Amount { get; set; }

        [Required(ErrorMessage = "The {0} field is required.")]
        [MaxLength(100, ErrorMessage = "The {0} field cannot exceed 100 characters.")]
        public string Provider { get; set; } = null!;   // e.g. "Stripe", "PayPal"

        /// <summary>
        /// How a payment the admin wrote on the job was made: "Card", "Cash" or "Bank transfer".
        /// Empty for the deposit paid on the website, which is always a card through Stripe.
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
