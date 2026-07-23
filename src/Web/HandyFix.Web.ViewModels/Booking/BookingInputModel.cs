namespace HandyFix.Web.ViewModels.Booking
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;

    using HandyFix.Web.ViewModels.Services;

    using Microsoft.AspNetCore.Http;

    public class BookingInputModel
    {
        [Required(ErrorMessage = "First name is required.")]
        [MinLength(2, ErrorMessage = "First name must be at least 2 characters.")]
        [MaxLength(100, ErrorMessage = "First name cannot exceed 100 characters.")]
        public string CustomerFirstName { get; set; }

        [Required(ErrorMessage = "Last name is required.")]
        [MinLength(2, ErrorMessage = "Last name must be at least 2 characters.")]
        [MaxLength(100, ErrorMessage = "Last name cannot exceed 100 characters.")]
        public string CustomerLastName { get; set; }

        [Required(ErrorMessage = "Email address is required.")]
        [EmailAddress(ErrorMessage = "Invalid email address format.")]
        [MaxLength(255, ErrorMessage = "Email cannot exceed 255 characters.")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Phone number is required.")]
        [Phone(ErrorMessage = "Invalid phone number format.")]
        [MaxLength(20, ErrorMessage = "Phone number cannot exceed 20 characters.")]
        public string PhoneNumber { get; set; }

        [Required(ErrorMessage = "Service address is required.")]
        [MinLength(10, ErrorMessage = "Address must be at least 10 characters long, including postal code.")]
        [MaxLength(300, ErrorMessage = "Address cannot exceed 300 characters.")]
        public string Address { get; set; }

        [Required(ErrorMessage = "Please describe the problem you need fixed.")]
        [MinLength(10, ErrorMessage = "Problem description must be at least 10 characters long.")]
        [MaxLength(3000, ErrorMessage = "Problem description cannot exceed 3000 characters.")]
        public string ProblemDescription { get; set; }

        [Required(ErrorMessage = "Please select an available appointment slot.")]
        public Guid SlotId { get; set; }

        [Required(ErrorMessage = "Please select a service.")]
        public Guid ServiceId { get; set; }

        public List<Guid> SelectedServiceIds { get; set; } = new List<Guid>();

        public List<IFormFile> Images { get; set; } = new List<IFormFile>();

        public IEnumerable<ServiceViewModel> Services { get; set; } = new List<ServiceViewModel>();

        public IEnumerable<DateTime> AvailableDates { get; set; } = new List<DateTime>();

        public string SelectedCategorySlug { get; set; }

        public Guid? SelectedServiceId { get; set; }

        public string SelectedDate { get; set; }
    }
}
