namespace HandyFix.Web.Areas.Administration.Controllers
{
    using System;
    using System.Threading.Tasks;

    using HandyFix.Services.Data.Reviews;
    using HandyFix.Web.ViewModels.Reviews;

    using Microsoft.AspNetCore.Mvc;

    public class ReviewsController : AdministrationController
    {
        private readonly IReviewsService reviewsService;

        public ReviewsController(IReviewsService reviewsService)
        {
            this.reviewsService = reviewsService;
        }

        public async Task<IActionResult> Index()
        {
            var reviews = await this.reviewsService.GetAllAsync<ReviewViewModel>();
            return this.View(reviews);
        }

        [HttpPost]
        public async Task<IActionResult> Approve(Guid id)
        {
            await this.reviewsService.ApproveReviewAsync(id);
            return this.RedirectToAction(nameof(this.Index));
        }

        [HttpPost]
        public async Task<IActionResult> Delete(Guid id)
        {
            await this.reviewsService.DeleteReviewAsync(id);
            return this.RedirectToAction(nameof(this.Index));
        }
    }
}
