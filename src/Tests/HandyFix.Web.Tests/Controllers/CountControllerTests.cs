namespace HandyFix.Web.Tests.Controllers
{
    using System;
    using System.Security.Claims;
    using System.Threading.Tasks;

    using HandyFix.Data.Models;
    using HandyFix.Services.Data.SiteCounts;
    using HandyFix.Web.Controllers;
    using HandyFix.Web.Services.Counts;

    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Logging.Abstractions;

    using Moq;

    using Xunit;

    // Where the pages' script says what a visitor did (PROJECT_STATE.md Section 3fa): which
    // word is which count, whose browser is counted, and that the answer is the same whatever
    // came in.
    public class CountControllerTests
    {
        private const string Computer = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36";

        private const string Phone = "Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.0 Mobile/15E148 Safari/604.1";

        // Half past midnight on 2 July in the UK, where the clocks are an hour ahead in summer;
        // still 1 July by the server's clock.
        private static readonly DateTimeOffset JustAfterMidnightInTheUk = new DateTimeOffset(2026, 7, 1, 23, 30, 0, TimeSpan.Zero);

        private readonly Mock<ISiteCountsService> siteCounts = new Mock<ISiteCountsService>();

        [Theory]
        [InlineData("visit", Computer, SiteCountKind.VisitOnComputer)]
        [InlineData("visit", Phone, SiteCountKind.VisitOnPhone)]
        [InlineData("booking", Computer, SiteCountKind.BookingOpened)]
        [InlineData("time", Phone, SiteCountKind.TimePicked)]
        [InlineData("call", Phone, SiteCountKind.CallTapped)]
        [InlineData("whatsapp", Computer, SiteCountKind.WhatsAppTapped)]
        public async Task AVisitorsWordIsAddedToTheBusinesssDay(string what, string userAgent, SiteCountKind expected)
        {
            CountController controller = this.ControllerFor(userAgent);

            IActionResult result = await controller.Add(what);

            Assert.IsType<NoContentResult>(result);
            this.siteCounts.Verify(x => x.AddAsync(expected, new DateOnly(2026, 7, 2)), Times.Once);
            this.siteCounts.VerifyNoOtherCalls();
        }

        [Theory]

        // A word the script does not send.
        [InlineData("everything", Computer)]
        [InlineData(null, Computer)]

        // Programs that read pages, by what they call themselves; one that says nothing.
        [InlineData("visit", "Mozilla/5.0 (compatible; Googlebot/2.1; +http://www.google.com/bot.html)")]
        [InlineData("visit", "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) HeadlessChrome/130.0.0.0 Safari/537.36")]
        [InlineData("call", "facebookexternalhit/1.1 (+http://www.facebook.com/externalhit_uatext.php)")]
        [InlineData("visit", "")]
        public async Task WhatIsNoCountOrComesFromARobotIsAnsweredTheSameAndNotAdded(string what, string userAgent)
        {
            CountController controller = this.ControllerFor(userAgent);

            Assert.IsType<NoContentResult>(await controller.Add(what));
            this.siteCounts.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task TheBusinesssOwnBrowsersAreNotCounted()
        {
            // Signed in to the admin panel.
            CountController signedIn = this.ControllerFor(Computer);
            signedIn.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "owner") }, "test"));
            await signedIn.Add("visit");

            // Signed out, in a browser the admin panel has marked.
            CountController marked = this.ControllerFor(Computer);
            marked.Request.Headers.Cookie = $"{SiteCounting.BrowserCookie}={SiteCounting.NotCounted}";
            await marked.Add("visit");

            this.siteCounts.VerifyNoOtherCalls();

            // The Dashboard's button puts a browser back.
            CountController putBack = this.ControllerFor(Computer);
            putBack.Request.Headers.Cookie = $"{SiteCounting.BrowserCookie}={SiteCounting.Counted}";
            await putBack.Add("visit");

            this.siteCounts.Verify(x => x.AddAsync(SiteCountKind.VisitOnComputer, It.IsAny<DateOnly>()), Times.Once);
        }

        // A browser says where a request comes from. One sent by a page of another site is not
        // this site's visitor doing something.
        [Theory]
        [InlineData("same-origin", 1)]
        [InlineData("cross-site", 0)]
        [InlineData("same-site", 0)]
        public async Task OnlyAPageOfThisSiteCanAddACount(string secFetchSite, int added)
        {
            CountController controller = this.ControllerFor(Computer);
            controller.Request.Headers["Sec-Fetch-Site"] = secFetchSite;

            await controller.Add("call");

            this.siteCounts.Verify(x => x.AddAsync(SiteCountKind.CallTapped, It.IsAny<DateOnly>()), Times.Exactly(added));
        }

        [Fact]
        public async Task ACountThatCannotBeSavedIsNotTheVisitorsProblem()
        {
            this.siteCounts
                .Setup(x => x.AddAsync(It.IsAny<SiteCountKind>(), It.IsAny<DateOnly>()))
                .ThrowsAsync(new InvalidOperationException("The database is out of reach."));
            CountController controller = this.ControllerFor(Computer);

            Assert.IsType<NoContentResult>(await controller.Add("visit"));
        }

        private CountController ControllerFor(string userAgent)
        {
            var httpContext = new DefaultHttpContext();
            if (userAgent != null)
            {
                httpContext.Request.Headers.UserAgent = userAgent;
            }

            return new CountController(this.siteCounts.Object, new FixedClock(JustAfterMidnightInTheUk), NullLogger<CountController>.Instance)
            {
                ControllerContext = new ControllerContext { HttpContext = httpContext },
            };
        }

        private sealed class FixedClock : TimeProvider
        {
            private readonly DateTimeOffset now;

            public FixedClock(DateTimeOffset now)
            {
                this.now = now;
            }

            public override DateTimeOffset GetUtcNow() => this.now;
        }
    }
}
