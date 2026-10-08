namespace HandyFix.Web.Services.Forms
{
    using System.Threading.Tasks;

    using Microsoft.AspNetCore.Http;

    // Tells a person's submission of a public form from a program's. Each public form renders
    // the _FormGuard partial, which adds what this checks (PROJECT_STATE.md Section 3cb).
    public interface IFormGuard
    {
        // The stamp the form carries: when it was first shown, signed so it cannot be made up.
        // A form shown again after a failed submission keeps the stamp it was sent with, so the
        // time is counted from when the person first saw it and a quick correction is not
        // mistaken for a program.
        string CreateStamp(HttpContext httpContext);

        // "form" is one of FormNames: it names the form in the log, and is the action its
        // Turnstile widget was shown with.
        Task<FormGuardResult> CheckAsync(HttpContext httpContext, string form);
    }
}
