namespace HandyFix.Web.Tests
{
    using Microsoft.Extensions.Hosting;

    using Moq;

    using Xunit;

    // The strip across the top of every page and the mark in the tab's title, which tell a copy of
    // the site from the live one (PROJECT_STATE.md Section 3cq). The test host runs as Development,
    // so a page test can only ever see a copy; the live site is checked here.
    public class EnvironmentMarkTests
    {
        [Fact]
        public void TheLiveSiteShowsNeitherTheStripNorTheMark()
        {
            IHostEnvironment live = Host(Environments.Production);

            Assert.Null(EnvironmentMark.Strip(live));
            Assert.Equal(string.Empty, EnvironmentMark.TitlePrefix(live));
        }

        [Fact]
        public void StagingShowsTheDevelopersOwnWordsAndItsNameInTheTitle()
        {
            IHostEnvironment staging = Host(Environments.Staging);

            Assert.Equal("TEST SITE (staging). Bookings, payments and emails here are not real.", EnvironmentMark.Strip(staging));
            Assert.Equal("[STAGING] ", EnvironmentMark.TitlePrefix(staging));
        }

        private static IHostEnvironment Host(string name)
        {
            return Mock.Of<IHostEnvironment>(x => x.EnvironmentName == name);
        }
    }
}
