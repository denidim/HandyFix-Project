namespace HandyFix.Web.Controllers
{
    using System;
    using System.Threading.Tasks;

    using HandyFix.Common;
    using HandyFix.Data.Models;
    using HandyFix.Services.Data.SiteCounts;
    using HandyFix.Web.Services.Counts;
    using HandyFix.Web.Services.Forms;

    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.RateLimiting;
    using Microsoft.Extensions.Logging;

    // Where the pages' script says that a visit was made, the booking page opened, a time picked
    // or Call or WhatsApp tapped (wwwroot/js/site.js, PROJECT_STATE.md Section 3fa). One is added
    // to that day's sum and nothing else is kept: no address, no browser, nothing about the
    // person.
    [AllowAnonymous]
    public class CountController : BaseController
    {
        private readonly ISiteCountsService siteCountsService;
        private readonly TimeProvider timeProvider;
        private readonly ILogger<CountController> logger;

        public CountController(ISiteCountsService siteCountsService, TimeProvider timeProvider, ILogger<CountController> logger)
        {
            this.siteCountsService = siteCountsService;
            this.timeProvider = timeProvider;
            this.logger = logger;
        }

        // The antiforgery check is off: the token it asks for is written into a form, and most
        // pages have no form. The check exists to stop another site from making a visitor's
        // browser do something as that visitor, and this does nothing as anybody. What it could
        // be misused for is padding the figures, which the limit on how often one address may
        // call it, and the rules of who is a visitor, are there for.
        //
        // The answer is the same whatever came in, and never an error: nothing here is the
        // visitor's to see or to put right.
        [HttpPost]
        [Route("count")]
        [IgnoreAntiforgeryToken]
        [EnableRateLimiting(RateLimits.CountsPolicy)]
        public async Task<IActionResult> Add(string what)
        {
            SiteCountKind? kind = SiteCounting.KindOf(what, this.Request.Headers.UserAgent.ToString());
            if (kind.HasValue && SiteCounting.IsAVisitor(this.HttpContext))
            {
                try
                {
                    DateTime now = UkTime.FromUtc(this.timeProvider.GetUtcNow().UtcDateTime);
                    await this.siteCountsService.AddAsync(kind.Value, DateOnly.FromDateTime(now));
                }
                catch (Exception ex)
                {
                    this.logger.LogError(ex, "A count of what a visitor did could not be saved");
                }
            }

            return this.NoContent();
        }
    }
}
