namespace HandyFix.Web.Services.Forms
{
    using System;
    using System.Globalization;
    using System.Security.Cryptography;
    using System.Threading.Tasks;

    using Microsoft.AspNetCore.DataProtection;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;

    public class FormGuard : IFormGuard
    {
        // A text box no person sees (it is moved off the page by forms.css and skipped by the
        // Tab key), so anything typed into it was typed by a program filling in every field. The
        // name is an ordinary-looking one that browsers do not fill in by themselves.
        public const string HoneypotFieldName = "Reference";

        public const string StampFieldName = "FormStamp";

        // The shortest time a person could take over the shortest of the forms. Read from
        // configuration when it is needed, so a test host can set its own.
        public const string MinimumSecondsKey = "FormGuard:MinimumSeconds";

        private const int DefaultMinimumSeconds = 3;

        private readonly IDataProtector protector;
        private readonly TimeProvider timeProvider;
        private readonly ITurnstileVerifier turnstile;
        private readonly IConfiguration configuration;
        private readonly ILogger<FormGuard> logger;

        public FormGuard(
            IDataProtectionProvider dataProtectionProvider,
            TimeProvider timeProvider,
            ITurnstileVerifier turnstile,
            IConfiguration configuration,
            ILogger<FormGuard> logger)
        {
            this.protector = dataProtectionProvider.CreateProtector("HandyFix.Web.FormGuard.Stamp.v1");
            this.timeProvider = timeProvider;
            this.turnstile = turnstile;
            this.configuration = configuration;
            this.logger = logger;
        }

        public string CreateStamp(HttpContext httpContext)
        {
            if (HttpMethods.IsPost(httpContext.Request.Method) && httpContext.Request.HasFormContentType)
            {
                var posted = httpContext.Request.Form[StampFieldName].ToString();
                if (this.ReadStamp(posted) != null)
                {
                    return posted;
                }
            }

            return this.protector.Protect(this.timeProvider.GetUtcNow().UtcTicks.ToString(CultureInfo.InvariantCulture));
        }

        public async Task<FormGuardResult> CheckAsync(HttpContext httpContext, string form)
        {
            IFormCollection posted = httpContext.Request.Form;

            if (!string.IsNullOrEmpty(posted[HoneypotFieldName].ToString()))
            {
                return this.Automated(form, "the hidden field was filled in");
            }

            DateTimeOffset? shownAt = this.ReadStamp(posted[StampFieldName].ToString());
            if (shownAt == null)
            {
                return this.Automated(form, "the stamp was missing or not one of ours");
            }

            TimeSpan taken = this.timeProvider.GetUtcNow() - shownAt.Value;
            if (taken < TimeSpan.FromSeconds(this.MinimumSeconds()))
            {
                return this.Automated(form, "it was sent faster than a person types");
            }

            // Last, because it is the one check that costs a call to another service: a
            // submission the two free checks above have already caught never gets this far.
            if (!await this.turnstile.VerifyAsync(httpContext, form))
            {
                return FormGuardResult.ChallengeFailed;
            }

            return FormGuardResult.Passed;
        }

        private int MinimumSeconds()
        {
            return int.TryParse(this.configuration[MinimumSecondsKey], NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds) && seconds >= 0
                ? seconds
                : DefaultMinimumSeconds;
        }

        // When the form was first shown, or null if the stamp is missing, altered or made up.
        private DateTimeOffset? ReadStamp(string stamp)
        {
            if (string.IsNullOrWhiteSpace(stamp))
            {
                return null;
            }

            try
            {
                var ticks = long.Parse(this.protector.Unprotect(stamp), CultureInfo.InvariantCulture);
                return new DateTimeOffset(ticks, TimeSpan.Zero);
            }
            catch (Exception ex) when (ex is CryptographicException || ex is FormatException || ex is ArgumentException || ex is OverflowException)
            {
                return null;
            }
        }

        // Logged so a real person caught by mistake leaves a trace: the form and the reason,
        // nothing of what was typed.
        private FormGuardResult Automated(string form, string reason)
        {
            this.logger.LogWarning("Submission of the {Form} form dropped as automated: {Reason}", form, reason);
            return FormGuardResult.Automated;
        }
    }
}
