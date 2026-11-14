namespace HandyFix.Web.Controllers
{
    using System.Threading.Tasks;

    using HandyFix.Services.Data.Gallery;
    using HandyFix.Web.Presentation;
    using HandyFix.Web.ViewModels.Gallery;

    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;

    [AllowAnonymous]
    public class GalleryController : BaseController
    {
        private readonly IGalleryService galleryService;

        public GalleryController(IGalleryService galleryService)
        {
            this.galleryService = galleryService;
        }

        // Reached by the conventional route, as /Gallery, like the Services page. An address of its
        // own here would win over the admin's Gallery page whenever that page sends the browser back
        // to its Index, and every admin action ended on this page (Section 3fd).
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            this.SetPageMeta(
                "Recent Plumbing & Handyman Work in Surrey",
                "Photos of real plumbing, handyman and small building jobs we completed across Surrey and South London. See our finish and book a slot online.",
                this.Url.Action("Index", "Gallery", null, this.Request.Scheme),
                PageTitle.BookOnline);

            this.ViewData["BodyClass"] = "page-bg-services-category";

            var model = new GalleryListViewModel
            {
                Photos = await this.galleryService.GetAllAsync(),
            };

            return this.View(model);
        }
    }
}
