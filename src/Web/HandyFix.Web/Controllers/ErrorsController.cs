namespace HandyFix.Web.Controllers
{
    using HandyFix.Web.ViewModels;

    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;

    // The site's own page for a request that ended in an error status (PROJECT_STATE Section
    // 3cb). Program.cs sends a browser here when a response comes back with an error status and
    // nothing in it (a page that is not there, a form sent too often, an expired form), and when
    // an exception was not handled.
    //
    // It takes no services on purpose: it has to be able to show itself when something the rest
    // of the site needs is what broke.
    [AllowAnonymous]
    public class ErrorsController : BaseController
    {
        // No verb is named and the antiforgery check is off, because the request arrives here as
        // it was first sent. A form whose token had expired comes as a POST with that same bad
        // token, and would be turned away again before it could be told what went wrong.
        [Route("StatusPage/{code:int}")]
        [IgnoreAntiforgeryToken]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult StatusPage(int code)
        {
            // Opened by its own address it would otherwise answer 200 "OK" under an error
            // message. Anything that is not an error status is treated as "not found".
            if (code < 400 || code > 599)
            {
                code = 404;
            }

            ViewResult page = this.View("StatusPage", StatusPageViewModel.For(code));
            page.StatusCode = code;
            return page;
        }
    }
}
