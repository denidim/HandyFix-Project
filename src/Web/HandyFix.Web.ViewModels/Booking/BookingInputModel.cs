namespace HandyFix.Web.ViewModels.Booking
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;

    using HandyFix.Web.ViewModels.Services;
    using HandyFix.Web.ViewModels.Validation;

    using Microsoft.AspNetCore.Http;

    public class BookingInputModel
    {
        [Required(ErrorMessage = "First name is required.")]
        [MinLength(2, ErrorMessage = "First name must be at least 2 characters.")]
        [MaxLength(100, ErrorMessage = "First name cannot exceed 100 characters.")]
        [PersonName]
        public string CustomerFirstName { get; set; }

        [Required(ErrorMessage = "Last name is required.")]
        [MinLength(2, ErrorMessage = "Last name must be at least 2 characters.")]
        [MaxLength(100, ErrorMessage = "Last name cannot exceed 100 characters.")]
        [PersonName]
        public string CustomerLastName { get; set; }

        [Required(ErrorMessage = "Email address is required.")]
        [StrictEmail]
        [MaxLength(255, ErrorMessage = "Email cannot exceed 255 characters.")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Phone number is required.")]
        [UkPhone]
        [MaxLength(20, ErrorMessage = "Phone number cannot exceed 20 characters.")]
        public string PhoneNumber { get; set; }

        // The street and town. The postcode has a box of its own below and is added to this when
        // the booking is saved, so 280 here leaves room for it in the column's 300.
        [Required(ErrorMessage = "Service address is required.")]
        [MinLength(5, ErrorMessage = "Address must be at least 5 characters long.")]
        [MaxLength(280, ErrorMessage = "Address cannot exceed 280 characters.")]
        public string Address { get; set; }

        // Its own field so it can be checked: for its form while the visitor types, and against
        // the districts the service areas list before a deposit is taken for a job that is too
        // far away (PROJECT_STATE Section 3cb).
        [Required(ErrorMessage = "Postcode is required.")]
        [UkPostcode]
        [MaxLength(10, ErrorMessage = "Postcode cannot exceed 10 characters.")]
        public string Postcode { get; set; }

        [Required(ErrorMessage = "Please describe the problem you need fixed.")]
        [MinLength(20, ErrorMessage = "Please tell us a little more: at least 20 characters.")]
        [MaxLength(3000, ErrorMessage = "Problem description cannot exceed 3000 characters.")]
        [NotMostlyLinks]
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

        public DateTime? SelectedDate { get; set; }

        // The districts bookings are taken in, for the page's own check as the postcode is typed.
        // Empty means no service area lists any, and then every postcode is accepted.
        public IEnumerable<string> ServedPostcodeDistricts { get; set; } = new List<string>();

        // Set when the server turned the booking down for its postcode, so the page can show the
        // way forward (an enquiry, a call) beside the field.
        public bool PostcodeNotServed { get; set; }
    }
}
