namespace HandyFix.Web.ViewModels.Administration.Technicians
{
    using System;
    using System.ComponentModel.DataAnnotations;

    using HandyFix.Services.Mapping;

    using IMapFromTechnician = HandyFix.Services.Mapping.IMapFrom<HandyFix.Data.Models.Technician>;

    public class TechnicianAdminInputModel : IMapFromTechnician
    {
        public Guid? Id { get; set; }

        [Required(ErrorMessage = "First name is required.")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "First name must be between 2 and 100 characters.")]
        [Display(Name = "First Name")]
        public string FirstName { get; set; }

        [Required(ErrorMessage = "Last name is required.")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Last name must be between 2 and 100 characters.")]
        [Display(Name = "Last Name")]
        public string LastName { get; set; }

        // Required here even though the column is nullable: a technician's number is what the
        // customer gets in the booking-confirmation email, so a roster entry without one isn't
        // useful. Tightening the form needs no migration.
        [Required(ErrorMessage = "Phone number is required - it goes to the customer on confirmation.")]
        [Phone(ErrorMessage = "Enter a valid phone number.")]
        [StringLength(20, ErrorMessage = "Phone number cannot exceed 20 characters.")]
        [Display(Name = "Phone Number")]
        public string PhoneNumber { get; set; }

        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;
    }
}
