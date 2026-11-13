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
        // Most services share this value, so they stay in alphabetical order behind the few
        // given a lower number.
        public const int DefaultDisplayOrder = 100;

        public Service()
        {
            this.Id = Guid.NewGuid();
            this.Images = new HashSet<ServiceImage>();
            this.BookingServices = new HashSet<BookingService>();
            this.Faqs = new HashSet<ServiceFaq>();
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

        // The service page's own blocks (PROJECT_STATE.md Section 3ep). Each may stay empty, and
        // an empty one is left off the page. The lists hold one point per line; a line of
        // CommonFindings or BeforeWeArrive may start with an icon name in square brackets.

        // One or two sentences for the service's card on a town page.
        [MaxLength(200, ErrorMessage = "The {0} field cannot exceed 200 characters.")]
        public string Summary { get; set; }

        // "What we usually find".
        [MaxLength(3000, ErrorMessage = "The {0} field cannot exceed 3000 characters.")]
        public string CommonFindings { get; set; }

        // "The visit, step by step".
        [MaxLength(3000, ErrorMessage = "The {0} field cannot exceed 3000 characters.")]
        public string VisitSteps { get; set; }

        // "Before we arrive".
        [MaxLength(2000, ErrorMessage = "The {0} field cannot exceed 2000 characters.")]
        public string BeforeWeArrive { get; set; }

        // "What is included".
        [MaxLength(2000, ErrorMessage = "The {0} field cannot exceed 2000 characters.")]
        public string Included { get; set; }

        // "What is not".
        [MaxLength(2000, ErrorMessage = "The {0} field cannot exceed 2000 characters.")]
        public string NotIncluded { get; set; }

        [Range(0.01, 10000, ErrorMessage = "The {0} must be between 0.01 and 10000.")]
        public decimal BasePrice { get; set; }

        [Range(15, 1440, ErrorMessage = "The {0} must be between 15 and 1440 minutes.")]
        public int EstimatedDurationMinutes { get; set; }

        public bool IsActive { get; set; } = true;

        // Every service list sorts by this, then by name. Lower comes first.
        [Range(0, 999, ErrorMessage = "The {0} must be between 0 and 999.")]
        public int DisplayOrder { get; set; } = DefaultDisplayOrder;

        // Shown in the home page's Popular Services.
        public bool IsPopular { get; set; }

        // Work on gas appliances, which UK law allows only a Gas Safe registered engineer to do.
        // The service page and its card show the Gas Safe logo. It is set by the migration that
        // added the boiler service, not in the admin form: the logo is the business's promise,
        // and an admin edit keeps it, since the edit writes only the fields the form has. Not
        // tied to the slug, which a rename in the admin changes.
        public bool IsGasSafeWork { get; set; }

        // Quoted after a survey rather than booked online as an hourly slot with a deposit.
        public bool IsQuoteOnly { get; set; }

        // Taken by phone and WhatsApp at any hour, never booked online: an emergency cannot wait
        // for the next free slot in the 9 to 5 calendar. Its page shows the call and WhatsApp
        // buttons, and its price is the emergency rate that the FAQ and the Terms quote.
        public bool IsEmergency { get; set; }

        [Required(ErrorMessage = "The {0} field is required.")]
        public Guid CategoryId { get; set; }

        public virtual ServiceCategory Category { get; set; } = null!;

        public virtual ICollection<ServiceImage> Images { get; set; }

        public virtual ICollection<BookingService> BookingServices { get; set; }

        public virtual ICollection<ServiceFaq> Faqs { get; set; }
    }
}
