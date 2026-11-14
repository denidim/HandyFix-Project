namespace HandyFix.Web.Areas.Administration.Controllers
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Services;
    using HandyFix.Services.Data.Gallery;
    using HandyFix.Web.ViewModels.Gallery;

    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Logging;

    public class GalleryController : AdministrationController
    {
        private readonly IGalleryService galleryService;
        private readonly ILogger<GalleryController> logger;

        public GalleryController(IGalleryService galleryService, ILogger<GalleryController> logger)
        {
            this.galleryService = galleryService;
            this.logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var model = new GalleryAdminIndexViewModel
            {
                Photos = await this.galleryService.GetAllAsync(),
            };

            return this.View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Upload(GalleryBatchUploadInputModel upload)
        {
            upload ??= new GalleryBatchUploadInputModel();

            int fileCount = upload.Photos?.Count(f => f != null && f.Length > 0) ?? 0;
            if (fileCount == 0)
            {
                this.ModelState.AddModelError("Upload.Photos", "Please select at least one photo to upload.");
            }
            else if (fileCount > GalleryService.MaxBatchUploadCount)
            {
                this.ModelState.AddModelError("Upload.Photos", $"A maximum of {GalleryService.MaxBatchUploadCount} photos can be uploaded in one batch.");
            }

            if (this.ModelState.IsValid)
            {
                try
                {
                    int savedCount = await this.galleryService.CreateBatchAsync(upload);
                    this.TempData["SuccessMessage"] = savedCount == 1
                        ? "Photo uploaded and added to the gallery."
                        : $"{savedCount} photos uploaded and added to the gallery.";
                    return this.RedirectToAction(nameof(this.Index));
                }
                catch (ImageUploadValidationException ex)
                {
                    this.ModelState.AddModelError("Upload.Photos", ex.Message);
                }
                catch (Exception ex)
                {
                    this.logger.LogError(ex, "Unexpected error uploading gallery batch photos");
                    this.ModelState.AddModelError("Upload.Photos", "The photos could not be uploaded right now. Please try again.");
                }
            }

            var indexModel = new GalleryAdminIndexViewModel
            {
                Photos = await this.galleryService.GetAllAsync(),
                Upload = upload,
            };

            return this.View(nameof(this.Index), indexModel);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(Guid id)
        {
            GalleryPhotoEditInputModel model = await this.galleryService.GetByIdAsync<GalleryPhotoEditInputModel>(id);
            if (model == null)
            {
                return this.NotFound();
            }

            return this.View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(GalleryPhotoEditInputModel model)
        {
            if (!this.ModelState.IsValid)
            {
                return this.View(model);
            }

            bool updated = await this.galleryService.UpdateAsync(model.Id, model);
            if (!updated)
            {
                return this.NotFound();
            }

            this.TempData["SuccessMessage"] = "Gallery photo updated.";
            return this.RedirectToAction(nameof(this.Index));
        }

        [HttpPost]
        public async Task<IActionResult> ToggleHome(Guid id)
        {
            bool? state = await this.galleryService.ToggleHomePageAsync(id);
            if (state == null)
            {
                return this.NotFound();
            }

            this.TempData["SuccessMessage"] = state.Value
                ? "Photo starred for the homepage strip."
                : "Photo removed from the homepage strip.";
            return this.RedirectToAction(nameof(this.Index));
        }

        [HttpPost]
        public async Task<IActionResult> Move(Guid id, int direction)
        {
            await this.galleryService.MoveAsync(id, direction);
            return this.RedirectToAction(nameof(this.Index));
        }

        [HttpPost]
        public async Task<IActionResult> Delete(Guid id)
        {
            bool deleted = await this.galleryService.DeleteAsync(id);
            if (!deleted)
            {
                return this.NotFound();
            }

            this.TempData["SuccessMessage"] = "Photo deleted from the gallery.";
            return this.RedirectToAction(nameof(this.Index));
        }
    }
}
