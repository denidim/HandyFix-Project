namespace HandyFix.Web.Tests.Presentation
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using HandyFix.Web.Presentation;

    using Xunit;

    // Which four services a town's page shows, and the short text on each card
    // (PROJECT_STATE.md Section 3el).
    public class AreaServicesTests
    {
        // As many services as the site had when the pick was written: 14 plumbing, 16 handyman.
        private static readonly IReadOnlyList<string> Plumbing = Enumerable.Range(0, 14).Select(i => "p" + i).ToList();
        private static readonly IReadOnlyList<string> Handyman = Enumerable.Range(0, 16).Select(i => "h" + i).ToList();

        // The towns of November 2026: the fifteen with a page and the six about to get one.
        private static readonly string[] Towns =
        {
            "chessington", "kingston-upon-thames", "epsom", "sutton", "cobham", "guildford", "surbiton",
            "worcester-park-ewell", "banstead", "esher", "leatherhead", "wimbledon",
            "walton-on-thames-weybridge", "dorking", "reigate", "twickenham", "richmond", "molesey",
            "new-malden", "ashtead", "cheam",
        };

        // Pinned on purpose. The pick has to come from the slug's letters alone: taken from
        // string.GetHashCode() it would change with every start of the site, and this test would
        // fail on most runs.
        [Theory]
        [InlineData("surbiton", "p13 p0 h9 h10")]
        [InlineData("esher", "p4 p5 h12 h13")]
        public void ATownsFourServicesAreTheSameOnEveryRun(string slug, string expected)
        {
            Assert.Equal(expected, string.Join(" ", AreaServices.For(slug, Plumbing, Handyman)));
        }

        [Fact]
        public void ATownShowsTwoPlumbingThenTwoHandymanServices()
        {
            foreach (string town in Towns)
            {
                IReadOnlyList<string> picked = AreaServices.For(town, Plumbing, Handyman);

                Assert.Equal(4, picked.Distinct().Count());
                Assert.All(picked.Take(2), service => Assert.StartsWith("p", service));
                Assert.All(picked.Skip(2), service => Assert.StartsWith("h", service));
            }
        }

        // What the change was for: the area pages no longer carry the same four cards.
        [Fact]
        public void NoTwoTownsShowTheSameFour()
        {
            var sets = Towns.Select(town => string.Join(" ", AreaServices.For(town, Plumbing, Handyman))).ToList();

            Assert.Equal(Towns.Length, sets.Distinct().Count());
        }

        [Fact]
        public void ACategoryWithTwoServicesOrFewerGivesWhatItHas()
        {
            Assert.Equal(new[] { "p0", "h0", "h1" }, AreaServices.For("cobham", new[] { "p0" }, new[] { "h0", "h1" }));
            Assert.Empty(AreaServices.For("cobham", Array.Empty<string>(), Array.Empty<string>()));
        }

        // The first sentence; the second with it when the first is under 60 characters. A dot
        // inside a number ends nothing, and a new paragraph is only a space.
        [Theory]
        [InlineData(
            "We fit new boilers and service existing ones. All boiler work is carried out by a Gas Safe registered engineer. A new boiler starts with a survey.",
            "We fit new boilers and service existing ones. All boiler work is carried out by a Gas Safe registered engineer.")]
        [InlineData(
            "Fixing leaking pipes, joints and fittings under sinks and around baths, toilets and radiators. We find the leak first.",
            "Fixing leaking pipes, joints and fittings under sinks and around baths, toilets and radiators.")]
        [InlineData(
            "Not sure which service you need? Book a handyman for an hour and tell us about the job. Good for small mixed jobs.",
            "Not sure which service you need? Book a handyman for an hour and tell us about the job.")]
        [InlineData(
            "A swap takes a day, a move 1.5 to 4 days and a full system about a week or longer. We quote first.",
            "A swap takes a day, a move 1.5 to 4 days and a full system about a week or longer.")]
        [InlineData(
            "First part.\n\nSecond part of the description, in a paragraph of its own. Third.",
            "First part. Second part of the description, in a paragraph of its own.")]
        [InlineData("Fixing dripping taps.", "Fixing dripping taps.")]
        [InlineData("", "")]
        [InlineData(null, "")]
        [InlineData("   ", "")]
        public void ACardCarriesTheStartOfTheDescription(string description, string expected)
        {
            Assert.Equal(expected, Summaries.Of(description));
        }
    }
}
