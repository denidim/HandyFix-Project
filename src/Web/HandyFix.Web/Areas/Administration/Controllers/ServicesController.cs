namespace HandyFix.Web.Areas.Administration.Controllers
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Services;
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
        private readonly IImageStorageService imageStorageService;

        public ServicesController(
            IServicesService servicesService,
            ICategoriesService categoriesService,
            IImageStorageService imageStorageService)
        {
            this.servicesService = servicesService;
            this.categoriesService = categoriesService;
            this.imageStorageService = imageStorageService;
        }

        public async Task<IActionResult> Index()
        {
            var services = await this.servicesService.GetAllAsync<ServiceViewModel>(activeOnly: false);
            return this.View(services);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var categories = await this.categoriesService.GetAllAsync<CategoryViewModel>();
            this.ViewData["Categories"] = new SelectList(categories, "Id", "Name");
            return this.View(new ServiceAdminInputModel());
        }

        [HttpPost]
        public async Task<IActionResult> Create(ServiceAdminInputModel model)
        {
            if (!this.ModelState.IsValid)
            {
                var categories = await this.categoriesService.GetAllAsync<CategoryViewModel>();
                this.ViewData["Categories"] = new SelectList(categories, "Id", "Name");
                return this.View(model);
            }

            var serviceId = await this.servicesService.CreateAsync(model.Name, model.Description, model.BasePrice, model.EstimatedDurationMinutes, model.CategoryId);

            if (model.ImageFile != null && model.ImageFile.Length > 0)
            {
                var service = await this.servicesService.GetByIdAsync<ServiceDetailsViewModel>(serviceId);
                if (service != null)
                {
                    try
                    {
                        using (var stream = model.ImageFile.OpenReadStream())
                        {
                            await this.imageStorageService.SaveServiceImageAsync(stream, model.ImageFile.FileName, model.ImageFile.ContentType, service.Slug);
                        }
                    }
                    catch (Exception ex)
                    {
                        this.ModelState.AddModelError("ImageFile", ex.Message);
                        var categories = await this.categoriesService.GetAllAsync<CategoryViewModel>();
                        this.ViewData["Categories"] = new SelectList(categories, "Id", "Name");
                        return this.View(model);
                    }
                }
            }

            return this.RedirectToAction(nameof(this.Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(Guid id)
        {
            var service = await this.servicesService.GetByIdAsync<ServiceDetailsViewModel>(id);
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

            var categories = await this.categoriesService.GetAllAsync<CategoryViewModel>();
            var activeCategory = categories.FirstOrDefault(c => c.Name == service.CategoryName);
            if (activeCategory != null)
            {
                model.CategoryId = activeCategory.Id;
            }

            this.ViewData["Categories"] = new SelectList(categories, "Id", "Name", model.CategoryId);
            return this.View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(ServiceAdminInputModel model)
        {
            if (!this.ModelState.IsValid)
            {
                var categories = await this.categoriesService.GetAllAsync<CategoryViewModel>();
                this.ViewData["Categories"] = new SelectList(categories, "Id", "Name", model.CategoryId);
                return this.View(model);
            }

            var oldService = await this.servicesService.GetByIdAsync<ServiceDetailsViewModel>(model.Id.Value);
            var oldSlug = oldService?.Slug;

            await this.servicesService.UpdateAsync(model.Id.Value, model.Name, model.Description, model.BasePrice, model.EstimatedDurationMinutes, model.IsActive, model.CategoryId);

            var newService = await this.servicesService.GetByIdAsync<ServiceDetailsViewModel>(model.Id.Value);
            var newSlug = newService?.Slug;

            if (oldSlug != null && newSlug != null)
            {
                try
                {
                    if (model.ImageFile != null && model.ImageFile.Length > 0)
                    {
                        // Delete old file if it exists, then save the new one
                        this.imageStorageService.DeleteServiceImage(oldSlug);
                        if (oldSlug != newSlug)
                        {
                            this.imageStorageService.DeleteServiceImage(newSlug);
                        }

                        using (var stream = model.ImageFile.OpenReadStream())
                        {
                            await this.imageStorageService.SaveServiceImageAsync(stream, model.ImageFile.FileName, model.ImageFile.ContentType, newSlug);
                        }
                    }
                    else if (oldSlug != newSlug)
                    {
                        // Slug changed but no new file uploaded: rename the existing file
                        this.imageStorageService.RenameServiceImage(oldSlug, newSlug);
                    }
                }
                catch (Exception ex)
                {
                    this.ModelState.AddModelError("ImageFile", ex.Message);
                    var categories = await this.categoriesService.GetAllAsync<CategoryViewModel>();
                    this.ViewData["Categories"] = new SelectList(categories, "Id", "Name", model.CategoryId);
                    return this.View(model);
                }
            }

            return this.RedirectToAction(nameof(this.Index));
        }

        [HttpPost]
        public async Task<IActionResult> Delete(Guid id)
        {
            var service = await this.servicesService.GetByIdAsync<ServiceDetailsViewModel>(id);
            if (service != null)
            {
                this.imageStorageService.DeleteServiceImage(service.Slug);
            }

            await this.servicesService.DeleteAsync(id);
            return this.RedirectToAction(nameof(this.Index));
        }
    }
}
