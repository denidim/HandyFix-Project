namespace HandyFix.Web.Areas.Administration.Controllers
{
    using System;
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

        public ServicesController(IServicesService servicesService, ICategoriesService categoriesService)
        {
            this.servicesService = servicesService;
            this.categoriesService = categoriesService;
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

            await this.servicesService.CreateAsync(model.Name, model.Description, model.BasePrice, model.EstimatedDurationMinutes, model.CategoryId);
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

            await this.servicesService.UpdateAsync(model.Id.Value, model.Name, model.Description, model.BasePrice, model.EstimatedDurationMinutes, model.IsActive, model.CategoryId);
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
