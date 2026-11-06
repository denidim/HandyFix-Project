namespace HandyFix.Services.Data.Emails
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Common;
    using HandyFix.Data.Models;
    using HandyFix.Services.Messaging;
    using HandyFix.Web.ViewModels.Home;

    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;

    // Every word of the emails about an enquiry or a job application (PROJECT_STATE.md Section
    // 3di). InquiriesService decides when they are sent: after the enquiry is saved, never before.
    public class EnquiryEmails : IEnquiryEmails
    {
        private readonly IEmailSender emailSender;
        private readonly IConfiguration configuration;
        private readonly ILogger<EnquiryEmails> logger;

        public EnquiryEmails(
            IEmailSender emailSender,
            IConfiguration configuration,
            ILogger<EnquiryEmails> logger)
        {
            this.emailSender = emailSender;
            this.configuration = configuration;
            this.logger = logger;
        }

        // To the company's one inbox, so an enquiry is seen without anyone opening the admin
        // panel. Its Reply-To is the sender's address: pressing Reply answers them, not the site.
        // Everything the sender typed is encoded before it goes into the HTML.
        public async Task<bool> NoticeToTheCompanyAsync(Inquiry inquiry, IReadOnlyList<string> imageUrls, int photosNotSaved)
        {
            var isApplication = IsJobApplication(inquiry);
            var what = isApplication ? "job application" : "enquiry";
            var name = EmailText.Encode(inquiry.Name);

            var photos = string.Empty;
            if (imageUrls != null && imageUrls.Count > 0)
            {
                // A photo's address ends in the name its file had, which can hold spaces.
                IEnumerable<string> links = imageUrls.Select((url, i) => $@"<a href=""{EmailText.Encode(url).Replace(" ", "%20")}"">Photo {i + 1}</a>");
                photos = EmailLayout.Text($"<strong>Photos:</strong> {string.Join(", ", links)}");
            }

            if (photosNotSaved > 0)
            {
                photos += EmailLayout.Note($"Photos attached but not saved: {photosNotSaved}.", "Storage could not be reached when this was sent. Please ask for them again if you need them.");
            }

            // A phone number is asked for on both forms, but an enquiry saved before that rule
            // may hold none, and a link to nowhere is worse than a plain word.
            var phone = string.IsNullOrWhiteSpace(inquiry.PhoneNumber)
                ? "Not given"
                : EmailLayout.Link("tel:" + EmailText.PhoneLink(inquiry.PhoneNumber), EmailText.Encode(inquiry.PhoneNumber));

            var body = EmailLayout.ForCompany(
                EmailLayout.Badge(EmailColour.Green, isApplication ? "&#128233; New Job Application" : "&#128233; New Website Enquiry")
                + EmailLayout.Heading($"A new {what} has arrived from the website.")
                + EmailLayout.Details(
                    EmailLayout.Row("Name", name),
                    EmailLayout.PlainRow("Email", EmailLayout.Link("mailto:" + EmailText.Encode(inquiry.Email), EmailText.Encode(inquiry.Email))),
                    EmailLayout.PlainRow("Phone", phone))
                + EmailLayout.Quote("Message Content:", EmailText.EncodeLines(inquiry.Message))
                + photos
                + EmailLayout.Advice($"&#128161; <strong>Quick Reply:</strong> Simply press <strong>Reply</strong> in your email app to reply directly to {name}.")
                + EmailLayout.Button("&#9881;&#65039; View Enquiries in Admin Panel", $"{EmailSettings.SiteUrl(this.configuration)}/Administration/Enquiries"));

            return await this.emailSender.TrySendEmailAsync(
                this.logger,
                $"{what} notice to the company",
                EmailSettings.SystemFromAddress(this.configuration),
                EmailSettings.WebsiteFromName,
                EmailSettings.AdminNotificationAddress(this.configuration),
                $"New {what} - {inquiry.Name}",
                body,
                replyTo: inquiry.Email);
        }

        // To the address the sender gave, so they know it arrived. It deliberately repeats
        // nothing they typed, not even their name: anyone can put any address into a public form,
        // and an email that echoed the form back would let the site be used to send a stranger
        // whatever text a bot chose, from the business's own domain.
        public async Task<bool> AcknowledgementToTheSenderAsync(Inquiry inquiry)
        {
            var isApplication = IsJobApplication(inquiry);
            var what = isApplication ? "application" : "enquiry";

            // Somebody asking for work has no leak to report, so the box about emergencies is
            // for an enquiry alone.
            var ifUrgent = isApplication
                ? string.Empty
                : EmailLayout.Note("Is your request urgent?", "If you require urgent assistance, an emergency repair, or active leak support, please call us directly or message us on WhatsApp for faster response.");

            var body = EmailLayout.ForCustomer(
                EmailLayout.Badge(EmailColour.Green, "&#10003; Message Received")
                + EmailLayout.Heading("Hello,")
                + EmailLayout.Lead($"Thank you for getting in touch with <strong>{GlobalConstants.SystemName}</strong>. We have received your {what} and our team will review the details and get back to you shortly.")
                + ifUrgent
                + EmailLayout.WhatsAppButton("Message Us on WhatsApp")
                + EmailLayout.SmallPrint($"If you did not submit an {what} on our website, you can safely disregard this email."));

            return await this.emailSender.TrySendEmailAsync(
                this.logger,
                $"{what} acknowledgement to the sender",
                EmailSettings.BookingsFromAddress(this.configuration),
                EmailSettings.CustomerFromName,
                inquiry.Email,
                $"We have received your {what}",
                body);
        }

        // A job application is saved as an enquiry whose message starts with a marker.
        private static bool IsJobApplication(Inquiry inquiry)
        {
            return inquiry.Message.StartsWith(JoinTeamInputModel.MessagePrefix, StringComparison.Ordinal);
        }
    }
}
