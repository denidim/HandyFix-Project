namespace HandyFix.Data.Models
{
    using System;
    using System.Collections.Generic;

    using System.ComponentModel.DataAnnotations;

    using HandyFix.Data.Common.Models;

    public class PaymentStatus : BaseDeletableModel<Guid>
    {
        public PaymentStatus()
        {
            this.Id = Guid.NewGuid();
            this.Payments = new HashSet<Payment>();
        }

        [Required(ErrorMessage = "The {0} field is required.")]
        [MinLength(3, ErrorMessage = "The {0} field must be at least 3 characters long.")]
        [MaxLength(50, ErrorMessage = "The {0} field cannot exceed 50 characters.")]
        public string Name { get; set; } = null!;

        public virtual ICollection<Payment> Payments { get; set; }
    }
}
