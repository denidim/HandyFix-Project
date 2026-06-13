namespace HandyFix.Data.Models
{
    using System;

    using System.Collections.Generic;

    using System.ComponentModel.DataAnnotations;

    using HandyFix.Data.Common.Models;

    public class Inquiry : BaseDeletableModel<Guid>
    {
        public Inquiry()
        {
            this.Id = Guid.NewGuid();
            this.Images = new HashSet<InquiryImage>();
        }

        [Required(ErrorMessage = "The {0} field is required.")]
        [MinLength(2, ErrorMessage = "The {0} field must be at least 2 characters long.")]
        [MaxLength(100, ErrorMessage = "The {0} field cannot exceed 100 characters.")]
        public string Name { get; set; } = null!;

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
        [MaxLength(3000, ErrorMessage = "The {0} field cannot exceed 3000 characters.")]
        public string Message { get; set; } = null!;

        public virtual ICollection<InquiryImage> Images { get; set; }
    }
}
