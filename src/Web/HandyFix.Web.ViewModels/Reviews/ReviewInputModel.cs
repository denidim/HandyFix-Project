namespace HandyFix.Web.ViewModels.Reviews
{
    using System.ComponentModel.DataAnnotations;

    public class ReviewInputModel
    {
        [Required(ErrorMessage = "Please enter your name.")]
        [MinLength(2, ErrorMessage = "Name must be at least 2 characters long.")]
        [MaxLength(100, ErrorMessage = "Name cannot exceed 100 characters.")]
        public string CustomerName { get; set; }

        [Required(ErrorMessage = "Please write a comment about our service.")]
        [MinLength(5, ErrorMessage = "Review comment must be at least 5 characters long.")]
        [MaxLength(1000, ErrorMessage = "Review comment cannot exceed 1000 characters.")]
        public string Comment { get; set; }

        [Range(1, 5, ErrorMessage = "Rating must be between 1 and 5 stars.")]
        public int Rating { get; set; } = 5;
    }
}
