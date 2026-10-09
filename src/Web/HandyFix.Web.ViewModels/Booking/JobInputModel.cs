namespace HandyFix.Web.ViewModels.Booking
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;

    using HandyFix.Data.Models;
    using HandyFix.Web.ViewModels.Services;

    /// <summary>
    /// A job the admin writes in by hand: one that came by phone, WhatsApp, an enquiry or an
    /// agency. A first name, a phone number, a day and an hour are enough
    /// (PROJECT_STATE.md Section 3ce); the website's own form, <see cref="BookingInputModel"/>,
    /// asks a customer for a good deal more.
    /// </summary>
    public class JobInputModel
    {
        [Required(ErrorMessage = "First name is required.")]
        [MinLength(2, ErrorMessage = "First name must be at least 2 characters.")]
        [MaxLength(100, ErrorMessage = "First name cannot exceed 100 characters.")]
        [Display(Name = "First name")]
        public string CustomerFirstName { get; set; }

        [MinLength(2, ErrorMessage = "Last name must be at least 2 characters.")]
        [MaxLength(100, ErrorMessage = "Last name cannot exceed 100 characters.")]
        [Display(Name = "Last name")]
        public string CustomerLastName { get; set; }

        // Looser than the website's rule on purpose: the admin types the number the customer or
        // the agency gave, which may be from abroad or an office line with an extension.
        [Required(ErrorMessage = "Phone number is required.")]
        [MaxLength(20, ErrorMessage = "Phone number cannot exceed 20 characters.")]
        [Display(Name = "Phone number")]
        public string PhoneNumber { get; set; }

        [EmailAddress(ErrorMessage = "This does not look like an email address.")]
        [MaxLength(255, ErrorMessage = "Email cannot exceed 255 characters.")]
        public string Email { get; set; }

        [MaxLength(300, ErrorMessage = "Address cannot exceed 300 characters.")]
        public string Address { get; set; }

        [Display(Name = "Service")]
        public Guid? ServiceId { get; set; }

        [MaxLength(3000, ErrorMessage = "This cannot exceed 3000 characters.")]
        [Display(Name = "What is the job")]
        public string ProblemDescription { get; set; }

        [Required(ErrorMessage = "Pick the day.")]
        [DataType(DataType.Date)]
        public DateTime? Date { get; set; }

        [Required(ErrorMessage = "Pick the time.")]
        [DataType(DataType.Time)]
        public TimeSpan? Time { get; set; }

        [Required(ErrorMessage = "Pick where the job came from.")]
        [Display(Name = "Came from")]
        public BookingSource? Source { get; set; }

        /// <summary>
        /// For the page only: the services the picker offers.
        /// </summary>
        public IEnumerable<ServiceViewModel> Services { get; set; } = new List<ServiceViewModel>();

        /// <summary>
        /// For the page only: the enquiry the form was opened from, so "Back" goes back to it.
        /// </summary>
        public Guid? EnquiryId { get; set; }
    }
}
