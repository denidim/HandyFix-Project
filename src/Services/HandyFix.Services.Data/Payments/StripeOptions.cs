namespace HandyFix.Services.Data.Payments
{
    // The "Stripe" section of the configuration. On the servers each value comes from the .env
    // file through the compose file; on a developer's machine there is none, and the site uses
    // the pretend payment.
    public class StripeOptions
    {
        public const string Section = "Stripe";

        // The restricted key the site opens, reads and closes payment pages with.
        public string SecretKey { get; set; }

        // The secret that proves a webhook message came from Stripe.
        public string WebhookSecret { get; set; }

        // Lets the pretend payment run outside Development, where a key is missing. The live
        // site must never have it (PROJECT_STATE.md Section 3cg); no server sets it now.
        public bool AllowSandboxOutsideDevelopment { get; set; }
    }
}
