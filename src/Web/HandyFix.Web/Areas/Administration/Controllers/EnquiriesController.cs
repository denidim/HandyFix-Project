namespace HandyFix.Web.Areas.Administration.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using HandyFix.Services.Data.Inquiries;
    using HandyFix.Web.ViewModels.Administration.Enquiries;

    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Logging;

    public class EnquiriesController : AdministrationController
    {
        private readonly IInquiriesService inquiriesService;
        private readonly ILogger<EnquiriesController> logger;

        public EnquiriesController(IInquiriesService inquiriesService, ILogger<EnquiriesController> logger)
        {
            this.inquiriesService = inquiriesService;
            this.logger = logger;
        }

        public async Task<IActionResult> Index(InquirySortField sortField = InquirySortField.CreatedOn, bool descending = true)
        {
            IEnumerable<EnquiryViewModel> inquiries = await this.inquiriesService.GetAllAsync<EnquiryViewModel>(sortField, descending);

            var model = new EnquiryListViewModel
            {
                Inquiries = inquiries,
                SortField = sortField,
                Descending = descending,
            };

            return this.View(model);
        }

        public async Task<IActionResult> Details(Guid id)
        {
            EnquiryViewModel inquiry = await this.inquiriesService.GetByIdAsync<EnquiryViewModel>(id);
            if (inquiry == null)
            {
                return this.NotFound();
            }

            return this.View(inquiry);
        }

        [HttpPost]
        public async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                await this.inquiriesService.DeleteAsync(id);
            }
            catch (Exception ex)
            {
                // The photos are removed from storage before the rows, and that is the step most
                // likely to fail. The enquiry is still there, so the admin goes back to it with a
                // message instead of an error page, and can press the button again.
                this.logger.LogError(ex, "Enquiry {EnquiryId} could not be deleted", id);
                this.TempData["ErrorMessage"] = "This enquiry could not be deleted. Please try again in a minute.";
                return this.RedirectToAction(nameof(this.Details), new { id });
            }

            this.TempData["SuccessMessage"] = "The enquiry and its photos have been deleted for good.";
            return this.RedirectToAction(nameof(this.Index));
        }
    }
}
