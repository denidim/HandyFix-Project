namespace HandyFix.Web.Areas.Administration.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Services.Data.Categories;
    using HandyFix.Services.Data.Services;
    using HandyFix.Web.ViewModels.Administration.Services;
    using HandyFix.Web.ViewModels.Services;

    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Rendering;

    public class ServicesController : AdministrationController
    {
        private readonly IServicesService servicesService;
        private readonly ICategoriesService categoriesService;

        public ServicesController(
            IServicesService servicesService,
            ICategoriesService categoriesService)
        {
            this.servicesService = servicesService;
            this.categoriesService = categoriesService;
        }

        public async Task<IActionResult> Index()
        {
            IEnumerable<ServiceViewModel> services = await this.servicesService.GetAllAsync<ServiceViewModel>(activeOnly: false);
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
            if (!this.ModelState.IsValid)
            {
                IEnumerable<CategoryViewModel> categories = await this.categoriesService.GetAllAsync<CategoryViewModel>();
                model.Categories = new SelectList(categories, "Id", "Name");
                return this.View(model);
            }

            Guid serviceId = await this.servicesService.CreateAsync(model.Name, model.Description, model.BasePrice, model.EstimatedDurationMinutes, model.CategoryId, model.DisplayOrder, model.IsPopular);

            try
            {
                await this.servicesService.SetServiceImageAsync(serviceId, model.ImageFile);
            }
            catch (Exception ex)
            {
                this.ModelState.AddModelError("ImageFile", ex.Message);
                IEnumerable<CategoryViewModel> categories = await this.categoriesService.GetAllAsync<CategoryViewModel>();
                model.Categories = new SelectList(categories, "Id", "Name");
                return this.View(model);
            }

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
            };

            IEnumerable<CategoryViewModel> categories = await this.categoriesService.GetAllAsync<CategoryViewModel>();
            CategoryViewModel activeCategory = categories.FirstOrDefault(c => c.Name == service.CategoryName);
            if (activeCategory != null)
            {
                model.CategoryId = activeCategory.Id;
            }

            model.Categories = new SelectList(categories, "Id", "Name", model.CategoryId);
            return this.View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(ServiceAdminInputModel model)
        {
            if (!this.ModelState.IsValid)
            {
                IEnumerable<CategoryViewModel> categories = await this.categoriesService.GetAllAsync<CategoryViewModel>();
                model.Categories = new SelectList(categories, "Id", "Name", model.CategoryId);
                return this.View(model);
            }

            ServiceDetailsViewModel oldService = await this.servicesService.GetByIdAsync<ServiceDetailsViewModel>(model.Id.Value);
            var oldSlug = oldService?.Slug;

            await this.servicesService.UpdateAsync(model.Id.Value, model.Name, model.Description, model.BasePrice, model.EstimatedDurationMinutes, model.IsActive, model.CategoryId, model.DisplayOrder, model.IsPopular);

            ServiceDetailsViewModel newService = await this.servicesService.GetByIdAsync<ServiceDetailsViewModel>(model.Id.Value);
            var newSlug = newService?.Slug;

            if (oldSlug != null && newSlug != null)
            {
                try
                {
                    await this.servicesService.UpdateServiceImageAsync(model.Id.Value, oldSlug, newSlug, model.ImageFile);
                }
                catch (Exception ex)
                {
                    this.ModelState.AddModelError("ImageFile", ex.Message);
                    IEnumerable<CategoryViewModel> categories = await this.categoriesService.GetAllAsync<CategoryViewModel>();
                    model.Categories = new SelectList(categories, "Id", "Name", model.CategoryId);
                    return this.View(model);
                }
            }

            return this.RedirectToList();
        }

        [HttpPost]
        public async Task<IActionResult> Delete(Guid id)
        {
            await this.servicesService.DeleteAsync(id);
            return this.RedirectToList();
        }

        // Controller and area are named in full. The public site has a ServicesController too,
        // with a fixed address of its own, and a redirect naming only the action resolved to that
        // one: every save or delete left the admin on the public /Services page, outside the
        // panel (PROJECT_STATE Section 3ca).
        private IActionResult RedirectToList()
        {
            return this.RedirectToAction(nameof(this.Index), "Services", new { area = "Administration" });
        }
    }
}
