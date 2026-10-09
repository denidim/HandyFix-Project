namespace HandyFix.Web.ViewModels.Booking
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;

    using HandyFix.Data.Models;
    using HandyFix.Web.ViewModels.Services;

    /// <summary>
    /// What the admin types about a job's customer and its work. "Write a job in"
    /// (<see cref="JobInputModel"/>) and "Edit the details" (<see cref="JobEditInputModel"/>)
    /// both stand on it, so the two forms cannot come to disagree about what a name, a phone
    /// number or an address may be (PROJECT_STATE.md Section 3cf).
    /// </summary>
    public class JobDetailsInputModel
    {
        /// <summary>
        /// Every way a job arrives except the website, which makes its own.
        /// </summary>
        public static readonly IReadOnlyList<BookingSource> WrittenInSources = new[]
        {
            BookingSource.Phone,
            BookingSource.WhatsApp,
            BookingSource.Enquiry,
            BookingSource.Agency,
            BookingSource.Other,
        };

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

        /// <summary>
        /// For the page only: the services the picker offers.
        /// </summary>
        public IEnumerable<ServiceViewModel> Services { get; set; } = new List<ServiceViewModel>();

        /// <summary>
        /// For the page only, and set from the job itself, never from what the form sends: a
        /// website booking's email is not optional, because the site emails that customer.
        /// Always false while a job is being written in.
        /// </summary>
        public bool CameFromWebsite { get; set; }
    }
}
