namespace HandyFix.Data.Models
{
    using System;
    using System.ComponentModel.DataAnnotations;

    using HandyFix.Data.Common.Models;

    public class ServiceAreaFaq : BaseDeletableModel<Guid>
    {
        public ServiceAreaFaq()
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
        public Guid ServiceAreaId { get; set; }

        public virtual ServiceArea ServiceArea { get; set; } = null!;
    }
}
