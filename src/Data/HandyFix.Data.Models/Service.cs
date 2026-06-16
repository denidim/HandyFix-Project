namespace HandyFix.Data.Models
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;

    using HandyFix.Data.Common.Models;
    using Microsoft.EntityFrameworkCore;

    [Index(nameof(Slug), IsUnique = true)]
    public class Service : BaseDeletableModel<Guid>
    {
        public Service()
        {
            this.Id = Guid.NewGuid();
            this.Images = new HashSet<ServiceImage>();
            this.BookingServices = new HashSet<BookingService>();
        }

        [Required(ErrorMessage = "The {0} field is required.")]
        [MinLength(3, ErrorMessage = "The {0} field must be at least 3 characters long.")]
        [MaxLength(100, ErrorMessage = "The {0} field cannot exceed 100 characters.")]
        public string Slug { get; set; } = null!;

        [Required(ErrorMessage = "The {0} field is required.")]
        [MinLength(3, ErrorMessage = "The {0} field must be at least 3 characters long.")]
        [MaxLength(100, ErrorMessage = "The {0} field cannot exceed 100 characters.")]
        public string Name { get; set; } = null!;

        [Required(ErrorMessage = "The {0} field is required.")]
        [MinLength(20, ErrorMessage = "The {0} field must be at least 20 characters long.")]
        [MaxLength(3000, ErrorMessage = "The {0} field cannot exceed 3000 characters.")]
        public string Description { get; set; } = null!;

        [Range(0.01, 10000, ErrorMessage = "The {0} must be between 0.01 and 10000.")]
        public decimal BasePrice { get; set; }

        [Range(15, 1440, ErrorMessage = "The {0} must be between 15 and 1440 minutes.")]
        public int EstimatedDurationMinutes { get; set; }

        public bool IsActive { get; set; } = true;

        [Required(ErrorMessage = "The {0} field is required.")]
        public Guid CategoryId { get; set; }

        public virtual ServiceCategory Category { get; set; } = null!;

        public virtual ICollection<ServiceImage> Images { get; set; }

        public virtual ICollection<BookingService> BookingServices { get; set; }
    }
}
