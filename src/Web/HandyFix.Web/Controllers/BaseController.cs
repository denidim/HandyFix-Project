namespace HandyFix.Web.Controllers
{
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;

    /// <summary>
    /// Authorization is opt-out from here down: every controller in the app inherits this
    /// (AdministrationController included, via its own additional Roles restriction), so a new
    /// controller is locked down by default and has to explicitly declare [AllowAnonymous] to be
    /// publicly reachable, rather than silently inheriting an unauthenticated, uncertain default.
    /// </summary>
    [Authorize]
    public class BaseController : Controller
    {
    }
}
