namespace HandyFix.Data.Models
{
    using System;
    using System.Collections.Generic;

    using System.ComponentModel.DataAnnotations;

    using HandyFix.Data.Common.Models;

    public class ServiceCategory : BaseDeletableModel<Guid>
    {
        public ServiceCategory()
        {
            this.Id = Guid.NewGuid();
            this.Services = new HashSet<Service>();
        }

        [Required(ErrorMessage = "The {0} field is required.")]
        [MinLength(3, ErrorMessage = "The {0} field must be at least 3 characters long.")]
        [MaxLength(100, ErrorMessage = "The {0} field cannot exceed 100 characters.")]
        public string Name { get; set; } = null!;

        [MinLength(10, ErrorMessage = "The {0} field must be at least 10 characters long.")]
        [MaxLength(500, ErrorMessage = "The {0} field cannot exceed 500 characters.")]
        public string Description { get; set; }

        public virtual ICollection<Service> Services { get; set; }
    }
}
