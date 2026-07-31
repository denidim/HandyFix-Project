namespace HandyFix.Web.Areas.Administration.Controllers
{
    using System;
    using System.Threading.Tasks;

    using HandyFix.Services.Data.Technicians;
    using HandyFix.Web.ViewModels.Administration.Technicians;

    using Microsoft.AspNetCore.Mvc;

    public class TechniciansController : AdministrationController
    {
        private readonly ITechniciansService techniciansService;

        public TechniciansController(ITechniciansService techniciansService)
        {
            this.techniciansService = techniciansService;
        }

        public async Task<IActionResult> Index()
        {
            var technicians = await this.techniciansService.GetAllAsync<TechnicianAdminListViewModel>();
            return this.View(technicians);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return this.View(new TechnicianAdminInputModel());
        }

        [HttpPost]
        public async Task<IActionResult> Create(TechnicianAdminInputModel model)
        {
            if (!this.ModelState.IsValid)
            {
                return this.View(model);
            }

            await this.techniciansService.CreateAsync(model);

            this.TempData["SuccessMessage"] = $"Technician \"{model.FirstName} {model.LastName}\" was added. They can now be assigned to bookings.";
            return this.RedirectToAction(nameof(this.Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(Guid id)
        {
            var model = await this.techniciansService.GetByIdAsync<TechnicianAdminInputModel>(id);
            if (model == null)
            {
                return this.NotFound();
            }

            return this.View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(TechnicianAdminInputModel model)
        {
            if (model.Id == null)
            {
                return this.NotFound();
            }

            if (!this.ModelState.IsValid)
            {
                return this.View(model);
            }

            var existing = await this.techniciansService.GetByIdAsync<TechnicianAdminInputModel>(model.Id.Value);
            if (existing == null)
            {
                return this.NotFound();
            }

            await this.techniciansService.UpdateAsync(model.Id.Value, model);

            this.TempData["SuccessMessage"] = existing.IsActive && !model.IsActive
                ? $"Technician \"{model.FirstName} {model.LastName}\" was deactivated. Existing bookings keep their assignment, but they can't be assigned to new ones."
                : $"Technician \"{model.FirstName} {model.LastName}\" was updated.";

            return this.RedirectToAction(nameof(this.Index));
        }

        [HttpPost]
        public async Task<IActionResult> Delete(Guid id)
        {
            var technician = await this.techniciansService.GetByIdAsync<TechnicianAdminInputModel>(id);
            if (technician == null)
            {
                return this.NotFound();
            }

            var deleted = await this.techniciansService.DeleteAsync(id);

            this.TempData[deleted ? "SuccessMessage" : "ErrorMessage"] = deleted
                ? $"Technician \"{technician.FirstName} {technician.LastName}\" was deleted."
                : $"\"{technician.FirstName} {technician.LastName}\" has bookings assigned and can't be deleted - deactivate them instead so past jobs keep their history.";

            return this.RedirectToAction(nameof(this.Index));
        }
    }
}
