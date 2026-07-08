namespace HandyFix.Web.Controllers
{
    using System;
    using System.Threading.Tasks;

    using HandyFix.Services.Data.Reviews;
    using HandyFix.Web.ViewModels.Reviews;

    using Microsoft.AspNetCore.Mvc;

    public class ReviewsController : BaseController
    {
        private readonly IReviewsService reviewsService;

        public ReviewsController(IReviewsService reviewsService)
        {
            this.reviewsService = reviewsService;
        }

        [HttpPost]
        [Route("Reviews/Submit")]
        public async Task<IActionResult> Submit(ReviewInputModel model)
        {
            if (!this.ModelState.IsValid)
            {
                this.TempData["ErrorMessage"] = "Failed to submit review. Please ensure all inputs are valid.";
                return this.RedirectToAction("Reviews", "Home");
            }

            await this.reviewsService.AddReviewAsync(model.CustomerName, model.Comment, model.Rating);
            this.TempData["SuccessMessage"] = "Thank you! Your review has been submitted for approval and will be visible shortly.";

            return this.RedirectToAction("Reviews", "Home");
        }
    }
}
