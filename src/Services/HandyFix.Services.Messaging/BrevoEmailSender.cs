namespace HandyFix.Services.Messaging
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading.Tasks;

    public class BrevoEmailSender : IEmailSender
    {
        private const string ApiUrl = "https://api.brevo.com/v3/smtp/email";

        private static readonly HttpClient HttpClient = new HttpClient();

        private readonly string apiKey;

        public BrevoEmailSender(string apiKey)
        {
            this.apiKey = apiKey;
        }

        public async Task SendEmailAsync(string from, string fromName, string to, string subject, string htmlContent, IEnumerable<EmailAttachment> attachments = null)
        {
            if (string.IsNullOrWhiteSpace(subject) && string.IsNullOrWhiteSpace(htmlContent))
            {
                throw new ArgumentException("Subject and message should be provided.");
            }

            var payload = new BrevoEmailRequest
            {
                Sender = new BrevoContact { Email = from, Name = fromName },
                To = new[] { new BrevoContact { Email = to } },
                Subject = subject,
                HtmlContent = htmlContent,
                Attachment = attachments?.Any() == true
                    ? attachments.Select(a => new BrevoAttachment
                    {
                        Content = Convert.ToBase64String(a.Content),
                        Name = a.FileName,
                    }).ToArray()
                    : null,
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, ApiUrl);
            request.Headers.Add("api-key", this.apiKey);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var response = await HttpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException($"Brevo email send failed ({(int)response.StatusCode}): {body}");
            }
        }

        private class BrevoEmailRequest
        {
            [JsonPropertyName("sender")]
            public BrevoContact Sender { get; set; }

            [JsonPropertyName("to")]
            public BrevoContact[] To { get; set; }

            [JsonPropertyName("subject")]
            public string Subject { get; set; }

            [JsonPropertyName("htmlContent")]
            public string HtmlContent { get; set; }

            [JsonPropertyName("attachment")]
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public BrevoAttachment[] Attachment { get; set; }
        }

        private class BrevoContact
        {
            [JsonPropertyName("email")]
            public string Email { get; set; }

            [JsonPropertyName("name")]
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public string Name { get; set; }
        }

        private class BrevoAttachment
        {
            [JsonPropertyName("content")]
            public string Content { get; set; }

            [JsonPropertyName("name")]
            public string Name { get; set; }
        }
    }
}
