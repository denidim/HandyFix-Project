namespace HandyFix.Services.Data.Emails
{
    using System.Threading.Tasks;

    // The emails about the admin's own login. Each answers whether the email went, and none of
    // them throws.
    public interface IAccountEmails
    {
        // To the admin's login address: a link to choose a new password, which works for
        // linkHours hours and only once.
        Task<bool> PasswordResetLinkAsync(string to, string link, int linkHours);
    }
}
