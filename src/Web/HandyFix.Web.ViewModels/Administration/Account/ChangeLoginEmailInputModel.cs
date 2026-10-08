namespace HandyFix.Web.ViewModels.Administration.Account
{
    using System.ComponentModel.DataAnnotations;

    using HandyFix.Web.ViewModels.Validation;

    public class ChangeLoginEmailInputModel
    {
        [Required(ErrorMessage = "Enter the new email address.")]
        [StrictEmail]
        [StringLength(255, ErrorMessage = "The email address cannot exceed 255 characters.")]
        [Display(Name = "New Login Email")]
        public string NewEmail { get; set; }

        // Asked for again: an admin panel left open must not be enough to move the login, and
        // with it the password reset email, to somebody else's mailbox.
        [Required(ErrorMessage = "Enter your password to confirm the change.")]
        [DataType(DataType.Password)]
        [Display(Name = "Current Password")]
        public string CurrentPassword { get; set; }
    }
}
