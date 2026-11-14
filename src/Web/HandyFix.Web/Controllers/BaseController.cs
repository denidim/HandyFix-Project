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
        /// <summary>
        /// What a public page tells the layout about itself: its title, the description search
        /// engines show, and its one address. Every action used to set the three by hand, and a
        /// form shown again after an error lost the last two (PROJECT_STATE.md Section 3dj).
        /// </summary>
        /// <param name="title">The page's title, or null when its view names it.</param>
        /// <param name="description">The description for search engines and link previews.</param>
        /// <param name="canonical">The page's address without whatever the link added to it.</param>
        /// <param name="titleEnding">What follows the title, one of <see cref="PageTitle"/>'s endings; null leaves the firm's name there.</param>
        protected void SetPageMeta(string title, string description, string canonical, string titleEnding = null)
        {
            if (title != null)
            {
                this.ViewData["Title"] = title;
            }

            if (titleEnding != null)
            {
                this.ViewData["TitleEnding"] = titleEnding;
            }

            this.ViewData["MetaDescription"] = description;
            this.ViewData["Canonical"] = canonical;
        }
    }
}
