namespace HandyFix.Data.Models
{
    using System;

    using System.ComponentModel.DataAnnotations;

    using HandyFix.Data.Common.Models;

    public class BookingImage : BaseDeletableModel<Guid>
    {
        public BookingImage()
        {
            this.Id = Guid.NewGuid();
        }

        [Required(ErrorMessage = "The {0} field is required.")]
        [MinLength(10, ErrorMessage = "The {0} field must be at least 10 characters long.")]
        [MaxLength(500, ErrorMessage = "The {0} field cannot exceed 500 characters.")]
        public string ImageUrl { get; set; } = null!;

        [Required(ErrorMessage = "The {0} field is required.")]
        public Guid BookingId { get; set; }

        public virtual Booking Booking { get; set; } = null!;
    }
}
