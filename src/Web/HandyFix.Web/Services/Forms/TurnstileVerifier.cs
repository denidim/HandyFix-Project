namespace HandyFix.Web.Services.Forms
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Net.Http.Json;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;

    public class TurnstileVerifier : ITurnstileVerifier
    {
        public const string SiteKeyKey = "Turnstile:SiteKey";

        public const string SecretKeyKey = "Turnstile:SecretKey";

        // The field the widget adds to the form.
        public const string ResponseFieldName = "cf-turnstile-response";

        private const string VerifyUrl = "https://challenges.cloudflare.com/turnstile/v0/siteverify";

        // Cloudflare's own limit on a token's length. Anything longer was not made by the widget.
        private const int MaxTokenLength = 2048;

        // Cloudflare publishes secret keys for testing that always pass, always fail or always
        // say "already used". Their answers carry a made-up hostname and action.
        private static readonly Regex TestSecret = new Regex("^[123]x0+AA$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

        private readonly HttpClient httpClient;
        private readonly IConfiguration configuration;
        private readonly IHostEnvironment environment;
        private readonly ILogger<TurnstileVerifier> logger;

        public TurnstileVerifier(
            HttpClient httpClient,
            IConfiguration configuration,
            IHostEnvironment environment,
            ILogger<TurnstileVerifier> logger)
        {
            this.httpClient = httpClient;
            this.configuration = configuration;
            this.environment = environment;
            this.logger = logger;
        }

        public string SiteKey => this.ReadKeys().SiteKey;

        public async Task<bool> VerifyAsync(HttpContext httpContext, string action)
        {
            (string siteKey, string secretKey) = this.ReadKeys();
            if (siteKey == null)
            {
                return true;
            }

            var token = httpContext.Request.Form[ResponseFieldName].ToString();
            if (string.IsNullOrWhiteSpace(token) || token.Length > MaxTokenLength)
            {
                // Nothing to ask Cloudflare about: the widget had not finished, or was not there.
                return false;
            }

            SiteVerifyAnswer answer;
            try
            {
                var fields = new Dictionary<string, string> { ["secret"] = secretKey, ["response"] = token };
                var address = httpContext.Connection.RemoteIpAddress?.ToString();
                if (!string.IsNullOrEmpty(address))
                {
                    fields["remoteip"] = address;
                }

                using var content = new FormUrlEncodedContent(fields);
                using HttpResponseMessage response = await this.httpClient.PostAsync(VerifyUrl, content, httpContext.RequestAborted);
                response.EnsureSuccessStatusCode();
                answer = await response.Content.ReadFromJsonAsync<SiteVerifyAnswer>(httpContext.RequestAborted);
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException || ex is JsonException || ex is NotSupportedException)
            {
                // Cloudflare could not be asked. That is not the visitor's doing, and turning
                // every enquiry and booking away until it comes back would cost more than the
                // spam the other checks (the hidden field, the time, the limit) let through
                // meanwhile. So the submission passes, and the log says why.
                this.logger.LogError(ex, "Turnstile could not be reached; the {Form} submission was let through unchecked", action);
                return true;
            }

            if (answer == null || !answer.Success)
            {
                var codes = answer?.ErrorCodes ?? Array.Empty<string>();
                if (codes.Contains("invalid-input-secret") || codes.Contains("missing-input-secret"))
                {
                    // Not a bad visitor: a bad setting. Every submission is being turned away.
                    this.logger.LogError("Turnstile does not accept this site's secret key; check {SecretKeyKey}. Every form submission is being refused.", SecretKeyKey);
                }
                else
                {
                    this.logger.LogWarning("Turnstile turned a {Form} submission down: {ErrorCodes}", action, string.Join(", ", codes));
                }

                return false;
            }

            if (TestSecret.IsMatch(secretKey))
            {
                return true;
            }

            // A real token says which form it was earned on and on which site. One earned on
            // another form, or lifted from another site that used this site's public key, is not
            // good here.
            if (!string.Equals(answer.Action, action, StringComparison.Ordinal))
            {
                this.logger.LogWarning("Turnstile token for the {Form} form was earned on \"{Action}\"", action, answer.Action);
                return false;
            }

            if (!string.Equals(answer.Hostname, httpContext.Request.Host.Host, StringComparison.OrdinalIgnoreCase))
            {
                this.logger.LogWarning("Turnstile token for the {Form} form was earned on another site, \"{Hostname}\"", action, answer.Hostname);
                return false;
            }

            return true;
        }

        // Both keys, or neither. With neither the check is off, in Development only: a deployed
        // site with no keys must not quietly run its forms unprotected, so it fails, loudly, like
        // a missing email or payment key. One key without the other is a mistake anywhere.
        private (string SiteKey, string SecretKey) ReadKeys()
        {
            var siteKey = this.configuration[SiteKeyKey];
            var secretKey = this.configuration[SecretKeyKey];
            var hasSiteKey = !string.IsNullOrWhiteSpace(siteKey);
            var hasSecretKey = !string.IsNullOrWhiteSpace(secretKey);

            if (hasSiteKey && hasSecretKey)
            {
                return (siteKey.Trim(), secretKey.Trim());
            }

            if (hasSiteKey != hasSecretKey)
            {
                throw new InvalidOperationException($"Turnstile is half configured. Set both {SiteKeyKey} and {SecretKeyKey}, or neither.");
            }

            if (this.environment.IsDevelopment())
            {
                return (null, null);
            }

            throw new InvalidOperationException($"Turnstile is not configured for this environment. Set {SiteKeyKey} and {SecretKeyKey} before the public forms take submissions.");
        }

        private sealed class SiteVerifyAnswer
        {
            [JsonPropertyName("success")]
            public bool Success { get; set; }

            [JsonPropertyName("action")]
            public string Action { get; set; }

            [JsonPropertyName("hostname")]
            public string Hostname { get; set; }

            [JsonPropertyName("error-codes")]
            public string[] ErrorCodes { get; set; }
        }
    }
}
