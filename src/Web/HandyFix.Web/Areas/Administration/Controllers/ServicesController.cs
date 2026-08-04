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

            Guid serviceId = await this.servicesService.CreateAsync(model.Name, model.Description, model.BasePrice, model.EstimatedDurationMinutes, model.CategoryId);

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

            return this.RedirectToAction(nameof(this.Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(Guid id)
        {
            ServiceDetailsViewModel service = await this.servicesService.GetByIdAsync<ServiceDetailsViewModel>(id);
            if (service == null)
            {
                return this.NotFound();
            }

            var model = new ServiceAdminInputModel
            {
                Id = service.Id,
                Name = service.Name,
                Description = service.Description,
                BasePrice = service.BasePrice,
                EstimatedDurationMinutes = service.EstimatedDurationMinutes,
                Slug = service.Slug,
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

            await this.servicesService.UpdateAsync(model.Id.Value, model.Name, model.Description, model.BasePrice, model.EstimatedDurationMinutes, model.IsActive, model.CategoryId);

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

            return this.RedirectToAction(nameof(this.Index));
        }

        [HttpPost]
        public async Task<IActionResult> Delete(Guid id)
        {
            await this.servicesService.DeleteAsync(id);
            return this.RedirectToAction(nameof(this.Index));
        }
    }
}
