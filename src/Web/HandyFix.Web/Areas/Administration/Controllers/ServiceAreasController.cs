namespace HandyFix.Web.Areas.Administration.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Services.Data.ServiceAreas;
    using HandyFix.Web.ViewModels.ServiceAreas;

    using Microsoft.AspNetCore.Mvc;

    public class ServiceAreasController : AdministrationController
    {
        private readonly IServiceAreasService serviceAreasService;

        public ServiceAreasController(IServiceAreasService serviceAreasService)
        {
            this.serviceAreasService = serviceAreasService;
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

            await this.serviceAreasService.CreateAsync(model);

            this.TempData["SuccessMessage"] = $"Service area \"{model.Name}\" was created. Remember to add its hero image and coverage-map entry - see docs/WORKFLOW_SERVICE_AREAS.md.";
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
                return this.View(model);
            }

            ServiceAreaAdminInputModel existing = await this.serviceAreasService.GetByIdAsync<ServiceAreaAdminInputModel>(model.Id.Value);
            if (existing == null)
            {
                return this.NotFound();
            }

            await this.serviceAreasService.UpdateAsync(model.Id.Value, model);

            this.TempData["SuccessMessage"] = existing.Slug != model.Slug.Trim().ToLowerInvariant()
                ? $"Service area \"{model.Name}\" was updated. The slug changed from \"{existing.Slug}\" to \"{model.Slug}\" - the hero image file and coverage-map entry both need renaming to match."
                : $"Service area \"{model.Name}\" was updated.";

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
