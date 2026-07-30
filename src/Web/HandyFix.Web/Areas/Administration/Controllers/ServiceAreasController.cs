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
            var areas = await this.serviceAreasService.GetAllAsync<ServiceAreaAdminListViewModel>(featuredFirst: false);
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
            this.PruneAndValidateFaqs(model);

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
            var model = await this.serviceAreasService.GetByIdAsync<ServiceAreaAdminInputModel>(id);
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

            this.PruneAndValidateFaqs(model);

            if (await this.serviceAreasService.SlugExistsAsync(model.Slug, model.Id))
            {
                this.ModelState.AddModelError(nameof(model.Slug), "That slug is already taken. Slugs must be unique, including by areas that were previously deleted.");
            }

            if (!this.ModelState.IsValid)
            {
                this.EnsureAtLeastOneFaqRow(model);
                return this.View(model);
            }

            var existing = await this.serviceAreasService.GetByIdAsync<ServiceAreaAdminInputModel>(model.Id.Value);
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
            var area = await this.serviceAreasService.GetByIdAsync<ServiceAreaAdminInputModel>(id);
            if (area == null)
            {
                return this.NotFound();
            }

            await this.serviceAreasService.DeleteAsync(id);

            this.TempData["SuccessMessage"] = $"Service area \"{area.Name}\" was permanently deleted, freeing the slug \"{area.Slug}\" for reuse.";
            return this.RedirectToAction(nameof(this.Index));
        }

        /// <summary>
        /// Drops FAQ rows left entirely blank, then validates whatever survives. Order matters:
        /// pruning first means every error key matches the index the row will actually render at.
        /// ServiceAreaFaqInputModel carries no DataAnnotations precisely so the binder cannot raise
        /// errors against pre-prune indices - see the note on that class.
        /// </summary>
        private void PruneAndValidateFaqs(ServiceAreaAdminInputModel model)
        {
            model.Faqs ??= new List<ServiceAreaFaqInputModel>();

            model.Faqs = model.Faqs
                .Where(f => !string.IsNullOrWhiteSpace(f?.Question) || !string.IsNullOrWhiteSpace(f?.Answer))
                .ToList();

            for (var i = 0; i < model.Faqs.Count; i++)
            {
                var question = model.Faqs[i].Question?.Trim();
                var answer = model.Faqs[i].Answer?.Trim();

                // Limits mirror the ServiceAreaFaq entity, so a row that passes here cannot fail
                // at SaveChanges.
                if (string.IsNullOrWhiteSpace(question))
                {
                    this.ModelState.AddModelError($"Faqs[{i}].Question", "Question is required.");
                }
                else if (question.Length < 5 || question.Length > 300)
                {
                    this.ModelState.AddModelError($"Faqs[{i}].Question", "Question must be between 5 and 300 characters.");
                }

                if (string.IsNullOrWhiteSpace(answer))
                {
                    this.ModelState.AddModelError($"Faqs[{i}].Answer", "Answer is required.");
                }
                else if (answer.Length < 5 || answer.Length > 1000)
                {
                    this.ModelState.AddModelError($"Faqs[{i}].Answer", "Answer must be between 5 and 1000 characters.");
                }
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
