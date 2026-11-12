namespace HandyFix.Web.Areas.Administration.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Services;
    using HandyFix.Services.Data.Categories;
    using HandyFix.Services.Data.Services;
    using HandyFix.Web.ViewModels.Administration.Services;
    using HandyFix.Web.ViewModels.Services;

    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Rendering;
    using Microsoft.Extensions.Logging;

    public class ServicesController : AdministrationController
    {
        private const string NameTakenMessage = "Another service already has this name, or one that was deleted did. Please choose a different name.";

        // Create and Edit both save the service before its picture, so a picture that fails leaves
        // the rest of the admin's changes in place; a new one then opens on its Edit page.
        private const string PictureNotSavedMessage = "The picture could not be saved. The service itself is saved: try the picture again.";

        // {0} is the service's name. A service is switched off with the Active tick on its Edit page.
        private const string HasJobsMessage = "\"{0}\" has jobs and can't be deleted. Switch it off instead: untick \"Is Active Service\" on its Edit page, and its jobs keep it.";

        // Carries a new service's picture error from Create to its Edit page.
        private const string PictureErrorKey = "PictureError";

        private readonly IServicesService servicesService;
        private readonly ICategoriesService categoriesService;
        private readonly ILogger<ServicesController> logger;

        public ServicesController(
            IServicesService servicesService,
            ICategoriesService categoriesService,
            ILogger<ServicesController> logger)
        {
            this.servicesService = servicesService;
            this.categoriesService = categoriesService;
            this.logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            IEnumerable<ServiceAdminListViewModel> services = await this.servicesService.GetAllAsync<ServiceAdminListViewModel>(activeOnly: false);
            return this.View(services);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            IEnumerable<CategoryViewModel> categories = await this.categoriesService.GetAllAsync<CategoryViewModel>();
            var model = new ServiceAdminInputModel
            {
                Categories = new SelectList(categories, "Id", "Name"),
            };
            return this.View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Create(ServiceAdminInputModel model)
        {
            if (this.ModelState.IsValid && await this.servicesService.NameIsTakenAsync(model.Name))
            {
                this.ModelState.AddModelError(nameof(model.Name), NameTakenMessage);
            }

            if (!this.ModelState.IsValid)
            {
                IEnumerable<CategoryViewModel> categories = await this.categoriesService.GetAllAsync<CategoryViewModel>();
                model.Categories = new SelectList(categories, "Id", "Name");
                return this.View(model);
            }

            Guid serviceId = await this.servicesService.CreateAsync(model.Name, model.Description, model.BasePrice, model.EstimatedDurationMinutes, model.CategoryId, model.DisplayOrder, model.IsPopular, model.IsQuoteOnly, model.IsEmergency);

            try
            {
                await this.servicesService.SetServiceImageAsync(serviceId, model.ImageFile);
            }
            catch (Exception ex)
            {
                // The service is saved by now. The Create form used to come back, and sent again
                // it only said the name was taken. The admin goes to the new service's Edit page
                // instead, where the picture can be tried again, with the reason under the
                // picture box.
                string error = this.PictureError(ex, serviceId);
                this.TempData[PictureErrorKey] = ex is ImageUploadValidationException
                    ? $"The service is saved, but not its picture. {error}"
                    : error;
                return this.RedirectToAction(nameof(this.Edit), "Services", new { area = "Administration", id = serviceId });
            }

            this.TempData["SuccessMessage"] = $"Service \"{model.Name}\" was added.";
            return this.RedirectToList();
        }

        [HttpGet]
        public async Task<IActionResult> Edit(Guid id)
        {
            ServiceDetailsViewModel service = await this.servicesService.GetByIdAsync<ServiceDetailsViewModel>(id);
            if (service == null)
            {
                return this.NotFound();
            }

            // Every field the form posts back is copied here. IsActive was once left out, so the
            // form opened ticked for an inactive service and saving any change switched it back
            // on (PROJECT_STATE Section 3ca).
            var model = new ServiceAdminInputModel
            {
                Id = service.Id,
                Name = service.Name,
                Description = service.Description,
                BasePrice = service.BasePrice,
                EstimatedDurationMinutes = service.EstimatedDurationMinutes,
                Slug = service.Slug,
                IsActive = service.IsActive,
                DisplayOrder = service.DisplayOrder,
                IsPopular = service.IsPopular,
                IsQuoteOnly = service.IsQuoteOnly,
                IsEmergency = service.IsEmergency,
                CurrentImageUrl = service.ImageUrl,
            };

            IEnumerable<CategoryViewModel> categories = await this.categoriesService.GetAllAsync<CategoryViewModel>();
            CategoryViewModel activeCategory = categories.FirstOrDefault(c => c.Name == service.CategoryName);
            if (activeCategory != null)
            {
                model.CategoryId = activeCategory.Id;
            }

            model.Categories = new SelectList(categories, "Id", "Name", model.CategoryId);

            // A new service whose picture failed arrives here from Create, with the reason.
            if (this.TempData[PictureErrorKey] is string pictureError)
            {
                this.ModelState.AddModelError(nameof(model.ImageFile), pictureError);
            }

            return this.View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(ServiceAdminInputModel model)
        {
            if (this.ModelState.IsValid && await this.servicesService.NameIsTakenAsync(model.Name, model.Id))
            {
                this.ModelState.AddModelError(nameof(model.Name), NameTakenMessage);
            }

            if (!this.ModelState.IsValid)
            {
                IEnumerable<CategoryViewModel> categories = await this.categoriesService.GetAllAsync<CategoryViewModel>();
                model.Categories = new SelectList(categories, "Id", "Name", model.CategoryId);
                return this.View(model);
            }

            await this.servicesService.UpdateAsync(model.Id.Value, model.Name, model.Description, model.BasePrice, model.EstimatedDurationMinutes, model.IsActive, model.CategoryId, model.DisplayOrder, model.IsPopular, model.IsQuoteOnly, model.IsEmergency);

            // A rename no longer touches the picture; only a new upload does (PROJECT_STATE.md
            // Section 3cq).
            try
            {
                await this.servicesService.SetServiceImageAsync(model.Id.Value, model.ImageFile);
            }
            catch (Exception ex)
            {
                this.ModelState.AddModelError("ImageFile", this.PictureError(ex, model.Id.Value));
                IEnumerable<CategoryViewModel> categories = await this.categoriesService.GetAllAsync<CategoryViewModel>();
                model.Categories = new SelectList(categories, "Id", "Name", model.CategoryId);
                return this.View(model);
            }

            this.TempData["SuccessMessage"] = $"Service \"{model.Name}\" was updated.";
            return this.RedirectToList();
        }

        [HttpPost]
        public async Task<IActionResult> Delete(Guid id)
        {
            // The name is read first: it is gone once the service is.
            ServiceDetailsViewModel service = await this.servicesService.GetByIdAsync<ServiceDetailsViewModel>(id);

            // Refused for a service a job has. The list shows a lock in place of the button for
            // such a service, so this is a delete sent from a list opened before the job was made.
            var deleted = await this.servicesService.DeleteAsync(id);

            if (service != null)
            {
                this.TempData[deleted ? "SuccessMessage" : "ErrorMessage"] = deleted
                    ? $"Service \"{service.Name}\" was deleted."
                    : string.Format(HasJobsMessage, service.Name);
            }

            return this.RedirectToList();
        }

        // Each save above leaves a line on the list saying what it did, as service areas and
        // technicians do. Until PROJECT_STATE Section 3cr a saved service gave no sign at all.
        //
        // Controller and area are named in full. The public site has a ServicesController too,
        // with a fixed address of its own, and a redirect naming only the action resolved to that
        // one: every save or delete left the admin on the public /Services page, outside the
        // panel (PROJECT_STATE Section 3ca).
        private IActionResult RedirectToList()
        {
            return this.RedirectToAction(nameof(this.Index), "Services", new { area = "Administration" });
        }

        // What goes under the picture box when the picture cannot be saved. A file that is not a
        // picture the site takes is the admin's to put right, and its message is written for them.
        // Anything else (the bucket out of reach, the picture library failing) used to show its own
        // text there; it is logged and said in our words.
        private string PictureError(Exception ex, Guid serviceId)
        {
            if (ex is ImageUploadValidationException)
            {
                return ex.Message;
            }

            this.logger.LogError(ex, "The picture of service {ServiceId} could not be saved", serviceId);
            return PictureNotSavedMessage;
        }
    }
}
