namespace HandyFix.Data.Models
{
    using System;

    using System.ComponentModel.DataAnnotations;

    using HandyFix.Data.Common.Models;

    public class ServiceImage : BaseDeletableModel<Guid>
    {
        public ServiceImage()
        {
            this.Id = Guid.NewGuid();
        }

        [Required(ErrorMessage = "The {0} field is required.")]
        [MinLength(10, ErrorMessage = "The {0} field must be at least 10 characters long.")]
        [MaxLength(500, ErrorMessage = "The {0} field cannot exceed 500 characters.")]
        public string ImageUrl { get; set; } = null!;

        [Required(ErrorMessage = "The {0} field is required.")]
        public Guid ServiceId { get; set; }

        public virtual Service Service { get; set; } = null!;
    }
}
