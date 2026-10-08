namespace HandyFix.Services.Data.Tests
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using HandyFix.Services.Data.Common;
    using HandyFix.Services.Messaging;

    using Microsoft.Extensions.Configuration;

    using Moq;

    using Xunit;

    // Staging sends from the same address as the live site, so its emails carry a mark in the
    // subject and the live site's carry none (PROJECT_STATE.md Section 3cc).
    public class SubjectPrefixEmailSenderTests
    {
        [Fact]
        public async Task TheMarkGoesInFrontOfTheSubjectAndNothingElseChanges()
        {
            var inner = new Mock<IEmailSender>();
            var attachments = new List<EmailAttachment> { new EmailAttachment { FileName = "photo.jpg" } };
            var sender = new SubjectPrefixEmailSender(inner.Object, " [STAGING] ");

            await sender.SendEmailAsync(
                "bookings@example.com",
                "Plumbing Handyman Surrey",
                "ada@example.com",
                "We have received your enquiry",
                "<p>Thank you.</p>",
                attachments,
                "reply@example.com");

            inner.Verify(
                s => s.SendEmailAsync(
                    "bookings@example.com",
                    "Plumbing Handyman Surrey",
                    "ada@example.com",
                    "[STAGING] We have received your enquiry",
                    "<p>Thank you.</p>",
                    attachments,
                    "reply@example.com"),
                Times.Once);
            inner.VerifyNoOtherCalls();
        }

        // The live site sets no mark. An empty line in its settings file has to mean the same as
        // no line at all, or every real email would start with a stray space.
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void WithNoMarkSetThereIsNoneToAdd(string configured)
        {
            Assert.Null(EmailSettings.SubjectPrefix(ConfigurationWith(configured)));
        }

        [Fact]
        public void TheMarkIsReadFromItsSettingWithoutTheSpacesAroundIt()
        {
            Assert.Equal("[STAGING]", EmailSettings.SubjectPrefix(ConfigurationWith(" [STAGING] ")));
        }

        private static IConfiguration ConfigurationWith(string subjectPrefix)
        {
            return new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string> { ["Email:SubjectPrefix"] = subjectPrefix })
                .Build();
        }
    }
}
