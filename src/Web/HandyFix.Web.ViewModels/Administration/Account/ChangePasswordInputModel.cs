namespace HandyFix.Web.ViewModels.Administration.Account
{
    using System.ComponentModel.DataAnnotations;

    using HandyFix.Common;

    public class ChangePasswordInputModel
    {
        [Required(ErrorMessage = "Enter the password you signed in with.")]
        [DataType(DataType.Password)]
        [Display(Name = "Current Password")]
        public string CurrentPassword { get; set; }

        [Required(ErrorMessage = "Enter a new password.")]
        [StringLength(100, MinimumLength = GlobalConstants.PasswordMinimumLength, ErrorMessage = "The password must be at least {2} characters long.")]
        [DataType(DataType.Password)]
        [Display(Name = "New Password")]
        public string NewPassword { get; set; }

        [Required(ErrorMessage = "Type the new password again.")]
        [DataType(DataType.Password)]
        [Compare(nameof(NewPassword), ErrorMessage = "The two passwords are not the same.")]
        [Display(Name = "New Password Again")]
        public string ConfirmPassword { get; set; }
    }
}
