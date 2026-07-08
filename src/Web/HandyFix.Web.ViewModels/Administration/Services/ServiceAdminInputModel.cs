namespace HandyFix.Web.ViewModels.Administration.Services
{
    using System;
    using System.ComponentModel.DataAnnotations;

    public class ServiceAdminInputModel
    {
        public Guid? Id { get; set; }

        [Required(ErrorMessage = "Service name is required.")]
        [MinLength(3, ErrorMessage = "Name must be at least 3 characters.")]
        [MaxLength(100, ErrorMessage = "Name cannot exceed 100 characters.")]
        public string Name { get; set; }

        [Required(ErrorMessage = "Description is required.")]
        [MinLength(20, ErrorMessage = "Description must be at least 20 characters.")]
        [MaxLength(3000, ErrorMessage = "Description cannot exceed 3000 characters.")]
        public string Description { get; set; }

        [Range(0.01, 10000.00, ErrorMessage = "Price must be between 0.01 and 10000.00.")]
        public decimal BasePrice { get; set; }

        [Range(15, 1440, ErrorMessage = "Duration must be between 15 and 1440 minutes.")]
        public int EstimatedDurationMinutes { get; set; }

        public bool IsActive { get; set; } = true;

        [Required(ErrorMessage = "Category is required.")]
        public Guid CategoryId { get; set; }
    }
}
