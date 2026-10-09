namespace HandyFix.Services.Data.Tests
{
    // An email the mocked sender was asked to send, as BookingWorld.CaptureEmails keeps it.
    internal sealed class SentEmail
    {
        public string To { get; set; }

        public string Subject { get; set; }

        public string Body { get; set; }

        public string ReplyTo { get; set; }
    }
}
