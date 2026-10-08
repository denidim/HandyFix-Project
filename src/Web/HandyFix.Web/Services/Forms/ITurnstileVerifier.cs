namespace HandyFix.Web.Services.Forms
{
    using System.Threading.Tasks;

    using Microsoft.AspNetCore.Http;

    // Cloudflare Turnstile: the "are you a person" check on the public forms. A small widget in
    // the form does its work in the visitor's browser and adds a token to what the form sends;
    // the token is only worth something once Cloudflare has been asked about it from here
    // (PROJECT_STATE.md Section 3cb).
    public interface ITurnstileVerifier
    {
        // The public key the widget is shown with, or null when the check is switched off (no
        // keys, in Development only). The _FormGuard partial shows the widget when this is set.
        string SiteKey { get; }

        // Whether the token that came with this submission is a good one for this form. "action"
        // is the form's name as the widget was given it ("contact", "booking"), so a token earned
        // on one form cannot be spent on another. True when the check is switched off.
        Task<bool> VerifyAsync(HttpContext httpContext, string action);
    }
}
