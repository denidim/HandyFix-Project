namespace HandyFix.Data.Models
{
    using System;

    using System.ComponentModel.DataAnnotations;

    using HandyFix.Data.Common.Models;

    // A customer's Google review, copied into the admin panel word for word by a manager on the
    // day it is answered on Google (PROJECT_STATE.md Section 3eh). Shown as text on /Reviews, the
    // home page and the page of its own town, never as review data for search engines.
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

        // Google takes a review of up to 4,096 characters; the 1,000 this column held before
        // Section 3eh would have refused a long one.
        [Required(ErrorMessage = "The {0} field is required.")]
        [MinLength(5, ErrorMessage = "The {0} field must be at least 5 characters long.")]
        [MaxLength(4000, ErrorMessage = "The {0} field cannot exceed 4000 characters.")]
        public string Comment { get; set; } = null!;

        [Range(1, 5, ErrorMessage = "The {0} must be between 1 and 5.")]
        public int Rating { get; set; }

        public bool IsApproved { get; set; } = false;

        // Where the customer lives, named as our area page names it, so that page shows the
        // review. Empty when the review does not say.
        [MaxLength(100)]
        public string Town { get; set; }

        // The work that was done, in a few words ("Boiler service"). Empty when not known.
        [MaxLength(100)]
        public string JobDone { get; set; }

        // The day the review went up on Google. Null only on a review saved before Section 3eh.
        public DateTime? ReviewedOn { get; set; }

        public string UserId { get; set; }

        public virtual ApplicationUser User { get; set; }
    }
}
