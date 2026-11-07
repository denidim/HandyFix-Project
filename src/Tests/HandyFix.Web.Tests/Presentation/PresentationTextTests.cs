namespace HandyFix.Web.Tests.Presentation
{
    using HandyFix.Web.Presentation;

    using Xunit;

    // How the public pages write a service's time and draw its description
    // (PROJECT_STATE.md Section 3do).
    public class PresentationTextTests
    {
        // "1 Hours" was on every one-hour service until Section 3do.
        [Theory]
        [InlineData(30, "30 Mins")]
        [InlineData(45, "45 Mins")]
        [InlineData(60, "1 Hour")]
        [InlineData(61, "1 Hour")]
        [InlineData(90, "1.5 Hours")]
        [InlineData(120, "2 Hours")]
        [InlineData(480, "8 Hours")]
        public void HoursOrMinutesWritesOneHourInTheSingular(int minutes, string expected)
        {
            Assert.Equal(expected, Durations.HoursOrMinutes(minutes));
        }

        [Fact]
        public void ABlankLineStartsANewParagraph()
        {
            Assert.Equal(
                new[] { "First part.", "Second part.", "Third part." },
                Paragraphs.Of("First part.\n\nSecond part.\r\n\r\nThird part."));
        }

        // A single line break stays inside its paragraph; spaces on the blank line and extra
        // blank lines start one new paragraph, not empty ones.
        [Fact]
        public void ALineBreakStaysAndExtraBlankLinesMakeNoEmptyParagraph()
        {
            Assert.Equal(
                new[] { "One line\nand the next.", "After a gap." },
                Paragraphs.Of("  One line\nand the next.\n  \n\n\nAfter a gap.\n\n"));
        }

        // Every description written before Section 3do is one paragraph, and stays one.
        [Theory]
        [InlineData("Fixing dripping taps.", 1)]
        [InlineData("", 0)]
        [InlineData(null, 0)]
        [InlineData("   ", 0)]
        public void AnOldDescriptionIsOneParagraph(string text, int count)
        {
            Assert.Equal(count, Paragraphs.Of(text).Count);
        }
    }
}
