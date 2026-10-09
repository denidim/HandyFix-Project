namespace HandyFix.Services.Data.Payments
{
    using System;
    using System.Threading.Tasks;

    using Microsoft.Extensions.Configuration;

    using Stripe;
    using Stripe.Checkout;

    public class StripeGateway : IStripeGateway
    {
        private readonly IConfiguration configuration;
        private readonly Lazy<SessionService> sessions;

        public StripeGateway(IConfiguration configuration)
        {
            this.configuration = configuration;

            // Made at the first call, not at start-up: in Development the site runs without a
            // key, on the pretend payment, and a client cannot be made without one.
            this.sessions = new Lazy<SessionService>(() => new SessionService(new StripeClient(this.configuration["Stripe:SecretKey"])));
        }

        public Task<Session> CreateCheckoutAsync(SessionCreateOptions options)
        {
            return this.sessions.Value.CreateAsync(options);
        }

        public Task<Session> GetCheckoutAsync(string sessionId)
        {
            return this.sessions.Value.GetAsync(sessionId);
        }

        public async Task<Session> ExpireCheckoutAsync(string sessionId)
        {
            try
            {
                return await this.sessions.Value.ExpireAsync(sessionId);
            }
            catch (StripeException ex) when (ex.StripeError != null)
            {
                // Stripe answered, and refused: only an open page can be closed. This one was
                // paid or had already expired, and asking for it says which.
                return await this.sessions.Value.GetAsync(sessionId);
            }
        }

        public Event ReadEvent(string json, string signature, string webhookSecret)
        {
            // The signature is what proves the message is Stripe's. The version check is left
            // out on purpose: a webhook can be set to a newer version of Stripe's format than
            // this library was built for, and the library then refuses every message. The site
            // reads only the message's type and the payment page's id, and asks Stripe itself
            // whether that page was paid, so the rest of the format does not matter to it.
            return EventUtility.ConstructEvent(json, signature, webhookSecret, throwOnApiVersionMismatch: false);
        }
    }
}
