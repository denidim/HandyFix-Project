namespace HandyFix.Data.Models
{
    using System;

    using System.Collections.Generic;

    using System.ComponentModel.DataAnnotations;

    using HandyFix.Data.Common.Models;

    public class Technician : BaseDeletableModel<Guid>
    {
        public Technician()
        {
            this.Id = Guid.NewGuid();
            this.Bookings = new HashSet<Booking>();
        }

        [Required(ErrorMessage = "The {0} field is required.")]
        [MinLength(2, ErrorMessage = "The {0} field must be at least 2 characters long.")]
        [MaxLength(100, ErrorMessage = "The {0} field cannot exceed 100 characters.")]
        public string FirstName { get; set; } = null!;

        [Required(ErrorMessage = "The {0} field is required.")]
        [MinLength(2, ErrorMessage = "The {0} field must be at least 2 characters long.")]
        [MaxLength(100, ErrorMessage = "The {0} field cannot exceed 100 characters.")]
        public string LastName { get; set; } = null!;

        [Phone(ErrorMessage = "The {0} field is not a valid phone number.")]
        [MaxLength(20, ErrorMessage = "The {0} field cannot exceed 20 characters.")]
        public string PhoneNumber { get; set; } = null!;

        public bool IsActive { get; set; } = true;

        public virtual ICollection<Booking> Bookings { get; set; }
    }
}
