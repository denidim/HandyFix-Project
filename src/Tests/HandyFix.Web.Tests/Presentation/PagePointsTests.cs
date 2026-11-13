namespace HandyFix.Web.Tests.Presentation
{
    using System.Linq;

    using HandyFix.Web.Presentation;
    using HandyFix.Web.ViewModels.Services;

    using Xunit;

    // How a service page reads its list blocks, and which services it offers "in the same visit"
    // (PROJECT_STATE.md Section 3ep).
    public class PagePointsTests
    {
        [Fact]
        public void ALineTakesTheIconItNamesAndItsFirstSentenceApart()
        {
            var points = PagePoints.Of("[water_drop] A worn washer. The spout drips however hard the tap is closed.", PagePoints.FindingIcon);

            var point = Assert.Single(points);
            Assert.Equal("water_drop", point.Icon);
            Assert.Equal("A worn washer.", point.Lead);
            Assert.Equal("The spout drips however hard the tap is closed.", point.Rest);
        }

        [Fact]
        public void ALineWithoutAnIconTakesTheBlocksOwn()
        {
            var point = Assert.Single(PagePoints.Of("Clear the cupboard under the sink.", PagePoints.BeforeIcon));

            Assert.Equal(PagePoints.BeforeIcon, point.Icon);
            Assert.Equal("Clear the cupboard under the sink.", point.Lead);
            Assert.Equal(string.Empty, point.Rest);
        }

        // The admin form refuses an unknown name before it is saved. Should one reach the page
        // anyway, the block's own icon is drawn, never the name as a word.
        [Fact]
        public void AnUnknownIconNameIsReportedAndNeverDrawn()
        {
            const string text = "[tap] A tap.\n[water_drop] A drip.\n[tap] Another tap.";

            Assert.Equal(new[] { "tap" }, PagePoints.UnknownIcons(text));
            Assert.Equal(new[] { PagePoints.FindingIcon, "water_drop", PagePoints.FindingIcon }, PagePoints.Of(text, PagePoints.FindingIcon).Select(p => p.Icon));
        }

        // Typed in the admin's box on Windows, the lines end in "\r\n" and may have blank lines
        // and spaces between them.
        [Fact]
        public void BlankLinesAndSpacesAreDroppedAndIconNamesLeftOut()
        {
            Assert.Equal(
                new[] { "We turn the water off.", "We fit the new part." },
                PagePoints.LinesOf("  We turn the water off.\r\n\r\n   \r\n[build] We fit the new part.  \r\n"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" \n \r\n ")]
        public void AnEmptyBlockHasNoPoints(string text)
        {
            Assert.Empty(PagePoints.Of(text, PagePoints.FindingIcon));
            Assert.Empty(PagePoints.LinesOf(text));
            Assert.Empty(PagePoints.UnknownIcons(text));
        }

        [Fact]
        public void TheSameVisitServicesComeInTheChosenOrderAndOnlyWhileOnOffer()
        {
            var offered = new[]
            {
                new ServiceViewModel { Slug = "radiator-trv-replacement", Name = "Radiator & TRV Replacement" },
                new ServiceViewModel { Slug = "toilet-repairs", Name = "Toilet Repairs" },
            };

            // Silicone & Mastic Resealing is chosen too, but is switched off here.
            Assert.Equal(
                new[] { "toilet-repairs", "radiator-trv-replacement" },
                SameVisitServices.For("tap-repairs", offered).Select(s => s.Slug));
            Assert.Empty(SameVisitServices.For("kitchen-fitting-alterations", offered));
            Assert.Empty(SameVisitServices.For(null, offered));
        }

        [Fact]
        public void ACardTakesTheServicesOwnSummaryWhenItHasOne()
        {
            Assert.Equal("Our own summary.", Summaries.Of("  Our own summary. ", "The description. Its second sentence."));
            Assert.Equal(Summaries.Of("The description, long enough to stand alone as a card's text. Its second sentence."), Summaries.Of(null, "The description, long enough to stand alone as a card's text. Its second sentence."));
        }
    }
}
