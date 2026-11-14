namespace HandyFix.Data.Models
{
    using System;
    using System.ComponentModel.DataAnnotations;

    using HandyFix.Data.Common.Models;

    // A photo of completed work shown on the Gallery page and optionally in the homepage strip.
    // Not a deletable model: removing a photo deletes the row and both copies from the photo
    // bucket together.
    public class GalleryPhoto : BaseModel<Guid>
    {
        public GalleryPhoto()
        {
            this.Id = Guid.NewGuid();
        }

        [Required(ErrorMessage = "The {0} field is required.")]
        [MaxLength(500, ErrorMessage = "The {0} field cannot exceed 500 characters.")]
        public string ImageUrl { get; set; } = null!;

        [Required(ErrorMessage = "The {0} field is required.")]
        [MaxLength(90, ErrorMessage = "The {0} field cannot exceed 90 characters.")]
        public string Caption { get; set; } = null!;

        [MaxLength(10, ErrorMessage = "The {0} field cannot exceed 10 characters.")]
        public string StageBadge { get; set; }

        public bool ShowOnHomePage { get; set; }

        public int DisplayOrder { get; set; }
    }
}
