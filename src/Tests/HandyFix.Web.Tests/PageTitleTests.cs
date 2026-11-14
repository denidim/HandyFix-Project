namespace HandyFix.Web.Tests
{
    using HandyFix.Common;

    using Xunit;

    // A page names what follows its title; one that names nothing keeps the firm's name (the SEO
    // master plan's item 7). Nothing is decided by the title's length any more.
    public class PageTitleTests
    {
        [Fact]
        public void APageThatNamesNoEndingKeepsTheFirmsName()
        {
            Assert.Equal("Privacy Policy - Plumbing Handyman Surrey", PageTitle.Full("Privacy Policy", null));
        }

        [Fact]
        public void ABookablePageEndsWithBookOnlineHoweverLongItIs()
        {
            const string town = "Plumber & Handyman in Walton-on-Thames & Weybridge";

            Assert.Equal(town + " - Book Online", PageTitle.Full(town, PageTitle.BookOnline));
        }

        [Fact]
        public void TheEmergencyPageEndsWithTheCall()
        {
            Assert.Equal(
                "Emergency Plumbing in Surrey from £150 - Call or WhatsApp",
                PageTitle.Full("Emergency Plumbing in Surrey from £150", PageTitle.CallOrWhatsApp));
        }

        [Fact]
        public void ATitleThatSaysItAllStandsAlone()
        {
            const string survey = "Kitchen Fitting & Alterations in Surrey, Free Survey";

            Assert.Equal(survey, PageTitle.Full(survey, PageTitle.NoEnding));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("  ")]
        public void APageWithoutATitleIsNamedByTheFirm(string title)
        {
            Assert.Equal(GlobalConstants.SystemName, PageTitle.Full(title, PageTitle.BookOnline));
        }
    }
}
