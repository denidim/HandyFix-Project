namespace HandyFix.Services.Data.Tests
{
    using System.Collections.Generic;
    using System.Text.RegularExpressions;

    using HandyFix.Services.Messaging;

    // An email the mocked sender was asked to send, as BookingWorld.CaptureEmails keeps it.
    internal sealed class SentEmail
    {
        public string To { get; set; }

        public string Subject { get; set; }

        public string Body { get; set; }

        public string ReplyTo { get; set; }

        public IEnumerable<EmailAttachment> Attachments { get; set; }

        // What a person reads: the body without its tags, so without the addresses its links
        // and its logo point at.
        public string WhatIsRead()
        {
            return Regex.Replace(this.Body, "<[^>]+>", " ");
        }
    }
}
