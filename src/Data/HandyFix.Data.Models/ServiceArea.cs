namespace HandyFix.Data.Models
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;

    using HandyFix.Data.Common.Models;
    using Microsoft.EntityFrameworkCore;

    [Index(nameof(Slug), IsUnique = true)]
    public class ServiceArea : BaseDeletableModel<Guid>
    {
        public ServiceArea()
        {
            this.Id = Guid.NewGuid();
            this.Faqs = new HashSet<ServiceAreaFaq>();
        }

        [Required(ErrorMessage = "The {0} field is required.")]
        [MinLength(3, ErrorMessage = "The {0} field must be at least 3 characters long.")]
        [MaxLength(100, ErrorMessage = "The {0} field cannot exceed 100 characters.")]
        public string Slug { get; set; } = null!;

        [Required(ErrorMessage = "The {0} field is required.")]
        [MinLength(2, ErrorMessage = "The {0} field must be at least 2 characters long.")]
        [MaxLength(100, ErrorMessage = "The {0} field cannot exceed 100 characters.")]
        public string Name { get; set; } = null!;

        [Required(ErrorMessage = "The {0} field is required.")]
        [MaxLength(100, ErrorMessage = "The {0} field cannot exceed 100 characters.")]
        public string Region { get; set; } = null!;

        [Range(0, 180, ErrorMessage = "The {0} must be between 0 and 180 minutes.")]
        public int DriveTimeMinutes { get; set; }

        [Required(ErrorMessage = "The {0} field is required.")]
        [MinLength(20, ErrorMessage = "The {0} field must be at least 20 characters long.")]
        [MaxLength(2000, ErrorMessage = "The {0} field cannot exceed 2000 characters.")]
        public string IntroCopy { get; set; } = null!;

        [Required(ErrorMessage = "The {0} field is required.")]
        [MinLength(20, ErrorMessage = "The {0} field must be at least 20 characters long.")]
        [MaxLength(2000, ErrorMessage = "The {0} field cannot exceed 2000 characters.")]
        public string LocalNeighbourhoodsCopy { get; set; } = null!;

        public bool IsFeatured { get; set; }

        public int DisplayOrder { get; set; }

        public virtual ICollection<ServiceAreaFaq> Faqs { get; set; }
    }
}
