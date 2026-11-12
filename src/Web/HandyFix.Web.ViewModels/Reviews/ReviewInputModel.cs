namespace HandyFix.Web.ViewModels.Reviews
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;

    using HandyFix.Data.Models;
    using HandyFix.Services.Mapping;

    // The admin's Add Review and Edit Review form: a Google review typed in word for word.
    public class ReviewInputModel : IMapFrom<Review>
    {
        public Guid? Id { get; set; }

        public bool IsNew => this.Id == null;

        [Required(ErrorMessage = "The name is required.")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "The name must be between 2 and 100 characters.")]
        [Display(Name = "Name")]
        public string CustomerName { get; set; }

        [StringLength(100, ErrorMessage = "The town cannot exceed 100 characters.")]
        [Display(Name = "Town")]
        public string Town { get; set; }

        [StringLength(100, ErrorMessage = "The job cannot exceed 100 characters.")]
        [Display(Name = "Job Done")]
        public string JobDone { get; set; }

        [Range(1, 5, ErrorMessage = "Pick from 1 to 5 stars.")]
        [Display(Name = "Stars")]
        public int Rating { get; set; } = 5;

        [Required(ErrorMessage = "The review's text is required.")]
        [StringLength(4000, MinimumLength = 5, ErrorMessage = "The text must be between 5 and 4000 characters.")]
        [Display(Name = "Review Text")]
        public string Comment { get; set; }

        [Required(ErrorMessage = "The date on Google is required.")]
        [DataType(DataType.Date)]
        [Display(Name = "Date on Google")]
        public DateTime? ReviewedOn { get; set; }

        // Our area pages' town names, offered as the town is typed, so the review reaches the page
        // of its town. Filled by the controller, never posted back.
        public IEnumerable<string> TownSuggestions { get; set; } = Array.Empty<string>();
    }
}
