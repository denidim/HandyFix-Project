namespace HandyFix.Web.Tests
{
    using System.Collections.Concurrent;
    using System.Threading.Tasks;

    using HandyFix.Web.Services.Forms;

    using Microsoft.AspNetCore.Http;

    // Stands in for Cloudflare Turnstile in a full-stack test: switched on, with a site key, and
    // passing or failing as the test says. It keeps the form names it was asked about.
    public class FakeTurnstile : ITurnstileVerifier
    {
        public string SiteKey => "site-key-for-tests";

        public bool Passes { get; set; } = true;

        public ConcurrentQueue<string> AskedAbout { get; } = new ConcurrentQueue<string>();

        public Task<bool> VerifyAsync(HttpContext httpContext, string action)
        {
            this.AskedAbout.Enqueue(action);
            return Task.FromResult(this.Passes);
        }
    }
}
