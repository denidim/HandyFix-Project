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

        // Minutes by car from the Chessington base: 0 is the base itself. Worked out when the
        // area is created (PROJECT_STATE.md Section 3dn); the admin no longer types it.
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

        // The postcode districts this area covers, as "KT5, KT6, KT7". The booking form takes a
        // booking only for a postcode in a district some area lists. Optional: an area with none
        // adds nothing to that check.
        [MaxLength(200, ErrorMessage = "The {0} field cannot exceed 200 characters.")]
        public string PostcodeDistricts { get; set; }

        // The area's picture when the admin has uploaded one: its address in the photo bucket,
        // with its phone copy beside it. Empty for an area that uses the picture that came with
        // the site, or has none (PROJECT_STATE.md Section 3dl).
        [MaxLength(500, ErrorMessage = "The {0} field cannot exceed 500 characters.")]
        public string HeroImageUrl { get; set; }

        // Where the area sits on the coverage diagram: its compass direction from the
        // Chessington base in degrees, 0 = north, 90 = east. Empty means the area is left off
        // the diagram (PROJECT_STATE.md Section 3dm).
        [Range(0, 360, ErrorMessage = "The {0} must be between 0 and 360 degrees.")]
        public double? DiagramBearing { get; set; }

        // Which side of its dot the name goes on. Empty means chosen from the direction.
        public DiagramLabelSide? DiagramLabelSide { get; set; }

        // A shorter name for the diagram, where the full one would not fit ("Kingston" for
        // "Kingston upon Thames"). Empty means the full name.
        [MaxLength(40, ErrorMessage = "The {0} field cannot exceed 40 characters.")]
        public string DiagramLabel { get; set; }

        public virtual ICollection<ServiceAreaFaq> Faqs { get; set; }
    }
}
