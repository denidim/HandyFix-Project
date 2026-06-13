namespace HandyFix.Data.Models
{
    using System;

    using System.ComponentModel.DataAnnotations;

    using HandyFix.Data.Common.Models;

    public class Review : BaseDeletableModel<Guid>
    {
        public Review()
        {
            this.Id = Guid.NewGuid();
        }

        [Required(ErrorMessage = "The {0} field is required.")]
        [MinLength(2, ErrorMessage = "The {0} field must be at least 2 characters long.")]
        [MaxLength(100, ErrorMessage = "The {0} field cannot exceed 100 characters.")]
        public string CustomerName { get; set; } = null!;

        [Required(ErrorMessage = "The {0} field is required.")]
        [MinLength(5, ErrorMessage = "The {0} field must be at least 5 characters long.")]
        [MaxLength(1000, ErrorMessage = "The {0} field cannot exceed 1000 characters.")]
        public string Comment { get; set; } = null!;

        [Range(1, 5, ErrorMessage = "The {0} must be between 1 and 5.")]
        public int Rating { get; set; }

        public bool IsApproved { get; set; } = false;

        public string UserId { get; set; }

        public virtual ApplicationUser User { get; set; }
    }
}
