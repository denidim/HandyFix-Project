namespace HandyFix.Web.ViewModels.Administration.Account
{
    // The admin panel's Account page: the login email as it is now, and its two forms. Each form
    // has a model of its own under its own name, so one form's mistakes are not shown on the
    // other and the two "current password" boxes do not share a name.
    public class AdminAccountViewModel
    {
        public string LoginEmail { get; set; }

        public ChangeLoginEmailInputModel Email { get; set; } = new ChangeLoginEmailInputModel();

        public ChangePasswordInputModel Password { get; set; } = new ChangePasswordInputModel();
    }
}
