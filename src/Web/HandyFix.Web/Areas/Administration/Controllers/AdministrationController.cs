namespace HandyFix.Web.Areas.Administration.Controllers
{
    using HandyFix.Common;
    using HandyFix.Web.Controllers;
    using HandyFix.Web.Services.Counts;

    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Filters;

    [Authorize(Roles = GlobalConstants.AdministratorRoleName)]
    [Area("Administration")]
    public class AdministrationController : BaseController
    {
        // A browser that opens the admin panel is one of the business's own, and its looks at
        // the public site are not a visitor's. It is marked the first time, and from then on the
        // site's count leaves it out, signed in or not (PROJECT_STATE.md Section 3fa). The
        // Dashboard has a button that changes the mark, so it is set here only where there is
        // none.
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            if (!this.Request.Cookies.ContainsKey(SiteCounting.BrowserCookie))
            {
                SiteCounting.MarkBrowser(this.HttpContext, counted: false);
            }

            base.OnActionExecuting(context);
        }
    }
}
