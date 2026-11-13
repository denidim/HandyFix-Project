namespace HandyFix.Data.Models
{
    using System;
    using System.ComponentModel.DataAnnotations;

    using HandyFix.Data.Common.Models;

    // A question asked about one service, shown on its page (PROJECT_STATE.md Section 3ep). The
    // twin of ServiceAreaFaq, with the same limits, so the two admin forms check them alike.
    public class ServiceFaq : BaseDeletableModel<Guid>
    {
        public ServiceFaq()
        {
            this.Id = Guid.NewGuid();
        }

        [Required(ErrorMessage = "The {0} field is required.")]
        [MinLength(5, ErrorMessage = "The {0} field must be at least 5 characters long.")]
        [MaxLength(300, ErrorMessage = "The {0} field cannot exceed 300 characters.")]
        public string Question { get; set; } = null!;

        [Required(ErrorMessage = "The {0} field is required.")]
        [MinLength(5, ErrorMessage = "The {0} field must be at least 5 characters long.")]
        [MaxLength(1000, ErrorMessage = "The {0} field cannot exceed 1000 characters.")]
        public string Answer { get; set; } = null!;

        public int DisplayOrder { get; set; }

        [Required(ErrorMessage = "The {0} field is required.")]
        public Guid ServiceId { get; set; }

        public virtual Service Service { get; set; } = null!;
    }
}
