namespace HandyFix.Web.Areas.Administration.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Services;
    using HandyFix.Services.Data.ServiceAreas;
    using HandyFix.Web.Presentation;
    using HandyFix.Web.ViewModels.ServiceAreas;

    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Logging;

    public class ServiceAreasController : AdministrationController
    {
        private const string PictureNotSavedMessage = "The picture could not be saved. The area itself is saved: try the picture again.";

        // A new area whose picture failed is saved, and its Edit page opens with the reason.
        private const string PictureErrorKey = "AreaPictureError";

        private readonly IServiceAreasService serviceAreasService;
        private readonly IWebHostEnvironment environment;
        private readonly ILogger<ServiceAreasController> logger;

        public ServiceAreasController(IServiceAreasService serviceAreasService, IWebHostEnvironment environment, ILogger<ServiceAreasController> logger)
        {
            this.serviceAreasService = serviceAreasService;
            this.environment = environment;
            this.logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            IEnumerable<ServiceAreaAdminListViewModel> areas = await this.serviceAreasService.GetAllAsync<ServiceAreaAdminListViewModel>(featuredFirst: false);
            return this.View(areas);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return this.View(new ServiceAreaAdminInputModel
            {
                Faqs = new List<ServiceAreaFaqInputModel> { new ServiceAreaFaqInputModel() },
            });
        }

        [HttpPost]
        public async Task<IActionResult> Create(ServiceAreaAdminInputModel model)
        {
            this.ApplyFaqValidationErrors(model);

            if (await this.serviceAreasService.SlugExistsAsync(model.Slug))
            {
                this.ModelState.AddModelError(nameof(model.Slug), "That slug is already taken. Slugs must be unique, including by areas that were previously deleted.");
            }

            if (!this.ModelState.IsValid)
            {
                this.EnsureAtLeastOneFaqRow(model);
                return this.View(model);
            }

            Guid areaId = await this.serviceAreasService.CreateAsync(model);

            try
            {
                await this.serviceAreasService.SetHeroImageAsync(areaId, model.HeroImageFile);
            }
            catch (Exception ex)
            {
                // The area is saved by now, so the admin goes to its Edit page, where the picture
                // can be tried again, with the reason under the picture box (as for a service).
                string error = this.PictureError(ex, areaId);
                this.TempData[PictureErrorKey] = ex is ImageUploadValidationException
                    ? $"The area is saved, but not its picture. {error}"
                    : error;
                return this.RedirectToAction(nameof(this.Edit), new { id = areaId });
            }

            this.TempData["SuccessMessage"] = $"Service area \"{model.Name}\" was created. Remember to add its coverage-map entry - see docs/WORKFLOW_SERVICE_AREAS.md.";
            return this.RedirectToAction(nameof(this.Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(Guid id)
        {
            ServiceAreaAdminInputModel model = await this.serviceAreasService.GetByIdAsync<ServiceAreaAdminInputModel>(id);
            if (model == null)
            {
                return this.NotFound();
            }

            // Mapster does not reliably preserve collection ordering through ProjectToType, so the
            // saved order is restored here explicitly - same reasoning as ServiceAreaDetailsViewModel.
            model.Faqs = model.Faqs?.OrderBy(f => f.DisplayOrder).ToList() ?? new List<ServiceAreaFaqInputModel>();
            this.EnsureAtLeastOneFaqRow(model);
            this.SetCurrentPicture(model);

            if (this.TempData[PictureErrorKey] is string pictureError)
            {
                this.ModelState.AddModelError(nameof(model.HeroImageFile), pictureError);
            }

            return this.View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(ServiceAreaAdminInputModel model)
        {
            if (model.Id == null)
            {
                return this.NotFound();
            }

            this.ApplyFaqValidationErrors(model);

            if (await this.serviceAreasService.SlugExistsAsync(model.Slug, model.Id))
            {
                this.ModelState.AddModelError(nameof(model.Slug), "That slug is already taken. Slugs must be unique, including by areas that were previously deleted.");
            }

            if (!this.ModelState.IsValid)
            {
                this.EnsureAtLeastOneFaqRow(model);
                await this.ShowAgainAsync(model);
                return this.View(model);
            }

            ServiceAreaAdminInputModel existing = await this.serviceAreasService.GetByIdAsync<ServiceAreaAdminInputModel>(model.Id.Value);
            if (existing == null)
            {
                return this.NotFound();
            }

            await this.serviceAreasService.UpdateAsync(model.Id.Value, model);

            try
            {
                await this.serviceAreasService.SetHeroImageAsync(model.Id.Value, model.HeroImageFile);
            }
            catch (Exception ex)
            {
                this.ModelState.AddModelError(nameof(model.HeroImageFile), this.PictureError(ex, model.Id.Value));
                this.EnsureAtLeastOneFaqRow(model);
                await this.ShowAgainAsync(model);
                return this.View(model);
            }

            // An uploaded picture keeps its address whatever the slug; one that came with the site
            // is named after the old slug, so the area needs a picture uploaded.
            var slugChanged = existing.Slug != model.Slug.Trim().ToLowerInvariant();
            var pictureLost = slugChanged && existing.HeroImageUrl == null && (model.HeroImageFile == null || model.HeroImageFile.Length == 0);
            this.TempData["SuccessMessage"] = !slugChanged
                ? $"Service area \"{model.Name}\" was updated."
                : $"Service area \"{model.Name}\" was updated. The slug changed from \"{existing.Slug}\" to \"{model.Slug}\" - the coverage-map entry needs renaming to match."
                    + (pictureLost ? " Upload a picture for it too: the one that came with the site is named after the old slug." : string.Empty);

            return this.RedirectToAction(nameof(this.Index));
        }

        [HttpPost]
        public async Task<IActionResult> Delete(Guid id)
        {
            ServiceAreaAdminInputModel area = await this.serviceAreasService.GetByIdAsync<ServiceAreaAdminInputModel>(id);
            if (area == null)
            {
                return this.NotFound();
            }

            await this.serviceAreasService.DeleteAsync(id);

            this.TempData["SuccessMessage"] = $"Service area \"{area.Name}\" was permanently deleted, freeing the slug \"{area.Slug}\" for reuse.";
            return this.RedirectToAction(nameof(this.Index));
        }

        /// <summary>
        /// Pruning and the validation rules themselves live in ServiceAreasService.
        /// ServiceAreaFaqInputModel carries no DataAnnotations precisely so the binder cannot raise
        /// errors against pre-prune indices - see the note on that class - so this has to run
        /// explicitly rather than through the usual [ApiController]/ModelState pipeline.
        /// </summary>
        private void ApplyFaqValidationErrors(ServiceAreaAdminInputModel model)
        {
            foreach (ServiceAreaFaqValidationError error in this.serviceAreasService.PruneAndValidateFaqs(model))
            {
                this.ModelState.AddModelError(error.Key, error.Message);
            }
        }

        // The picture the area's page shows now, for the Edit page.
        private void SetCurrentPicture(ServiceAreaAdminInputModel model)
        {
            model.CurrentPictureUrl = PictureLookups.AreaPictureOf(this.environment, model.Slug, model.HeroImageUrl);
        }

        // The form posts no picture address, so the saved one is read again before the form is
        // shown again.
        private async Task ShowAgainAsync(ServiceAreaAdminInputModel model)
        {
            ServiceAreaAdminInputModel saved = await this.serviceAreasService.GetByIdAsync<ServiceAreaAdminInputModel>(model.Id.Value);
            model.HeroImageUrl = saved?.HeroImageUrl;
            this.SetCurrentPicture(model);
        }

        // What goes under the picture box when the picture cannot be saved: the admin's own words
        // for a file the site does not take, our words for anything else, which is logged.
        private string PictureError(Exception ex, Guid areaId)
        {
            if (ex is ImageUploadValidationException)
            {
                return ex.Message;
            }

            this.logger.LogError(ex, "The picture of service area {AreaId} could not be saved", areaId);
            return PictureNotSavedMessage;
        }

        private void EnsureAtLeastOneFaqRow(ServiceAreaAdminInputModel model)
        {
            model.Faqs ??= new List<ServiceAreaFaqInputModel>();
            if (model.Faqs.Count == 0)
            {
                model.Faqs.Add(new ServiceAreaFaqInputModel());
            }
        }
    }
}
