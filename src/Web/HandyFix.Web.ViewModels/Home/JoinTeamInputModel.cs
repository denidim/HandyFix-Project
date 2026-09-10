namespace HandyFix.Web.ViewModels.Home
{
    using System.ComponentModel.DataAnnotations;
    using System.Text;

    public class JoinTeamInputModel
    {
        // Marks a saved enquiry as a job application in the admin Enquiries list.
        public const string MessagePrefix = "[Job Application]";

        [Required(ErrorMessage = "Please enter your name.")]
        [MinLength(2, ErrorMessage = "Name must be at least 2 characters.")]
        [MaxLength(100, ErrorMessage = "Name cannot exceed 100 characters.")]
        public string Name { get; set; }

        [Required(ErrorMessage = "Please enter your email address.")]
        [EmailAddress(ErrorMessage = "Invalid email address format.")]
        [MaxLength(255, ErrorMessage = "Email cannot exceed 255 characters.")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Please enter your phone number.")]
        [Phone(ErrorMessage = "Invalid phone number format.")]
        [MaxLength(20, ErrorMessage = "Phone number cannot exceed 20 characters.")]
        public string PhoneNumber { get; set; }

        [Required(ErrorMessage = "Please choose your main trade.")]
        [MaxLength(50, ErrorMessage = "Trade cannot exceed 50 characters.")]
        public string Trade { get; set; }

        // Nullable so an empty field fails [Required] instead of silently binding as 0 years.
        [Required(ErrorMessage = "Please enter your years of experience.")]
        [Range(0, 60, ErrorMessage = "Years of experience must be between 0 and 60.")]
        public int? YearsExperience { get; set; }

        [Required(ErrorMessage = "Please choose your availability.")]
        [MaxLength(50, ErrorMessage = "Availability cannot exceed 50 characters.")]
        public string Availability { get; set; }

        public bool HasOwnTools { get; set; }

        public bool HasOwnTransport { get; set; }

        [MaxLength(2000, ErrorMessage = "This section cannot exceed 2000 characters.")]
        public string AboutYou { get; set; }

        // Applications are stored through the existing enquiry pipeline rather than a table of
        // their own, so the application's fields travel as one formatted message. The worst case
        // (all fields at their max) stays well inside Inquiry.Message's 3000-character limit.
        public ContactInputModel ToContactInputModel()
        {
            var message = new StringBuilder();
            message.AppendLine(MessagePrefix);
            message.AppendLine($"Trade: {this.Trade}");
            message.AppendLine($"Years of experience: {this.YearsExperience}");
            message.AppendLine($"Availability: {this.Availability}");
            message.AppendLine($"Own tools: {(this.HasOwnTools ? "Yes" : "No")}");
            message.AppendLine($"Own transport: {(this.HasOwnTransport ? "Yes" : "No")}");

            if (!string.IsNullOrWhiteSpace(this.AboutYou))
            {
                message.AppendLine();
                message.AppendLine("About the applicant:");
                message.AppendLine(this.AboutYou.Trim());
            }

            return new ContactInputModel
            {
                Name = this.Name,
                Email = this.Email,
                PhoneNumber = this.PhoneNumber,
                Message = message.ToString().TrimEnd(),
            };
        }
    }
}
