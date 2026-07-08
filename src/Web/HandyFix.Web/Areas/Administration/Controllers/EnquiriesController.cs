namespace HandyFix.Web.Areas.Administration.Controllers
{
    using System;
    using System.Threading.Tasks;

    using HandyFix.Services.Data.Inquiries;
    using HandyFix.Web.ViewModels.Administration.Enquiries;

    using Microsoft.AspNetCore.Mvc;

    public class EnquiriesController : AdministrationController
    {
        private readonly IInquiriesService inquiriesService;

        public EnquiriesController(IInquiriesService inquiriesService)
        {
            this.inquiriesService = inquiriesService;
        }

        public async Task<IActionResult> Index()
        {
            var inquiries = await this.inquiriesService.GetAllAsync<EnquiryViewModel>();
            return this.View(inquiries);
        }

        public async Task<IActionResult> Details(Guid id)
        {
            var inquiry = await this.inquiriesService.GetByIdAsync<EnquiryViewModel>(id);
            if (inquiry == null)
            {
                return this.NotFound();
            }

            return this.View(inquiry);
        }

        [HttpPost]
        public async Task<IActionResult> Delete(Guid id)
        {
            await this.inquiriesService.DeleteAsync(id);
            return this.RedirectToAction(nameof(this.Index));
        }
    }
}
