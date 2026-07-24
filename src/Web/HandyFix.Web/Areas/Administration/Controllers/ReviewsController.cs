namespace HandyFix.Web.Areas.Administration.Controllers
{
    using System;
    using System.Linq;
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

        public async Task<IActionResult> Index(ReviewSortField sortField = ReviewSortField.CreatedOn, bool descending = true, string status = null)
        {
            var reviews = (await this.reviewsService.GetAllAsync<ReviewViewModel>(sortField, descending, status)).ToList();

            // Summary stats always reflect the whole business, not just whatever status
            // filter is currently applied to the table below.
            var allReviews = string.IsNullOrWhiteSpace(status)
                ? reviews
                : (await this.reviewsService.GetAllAsync<ReviewViewModel>()).ToList();

            var model = new ReviewListViewModel
            {
                Reviews = reviews,
                SortField = sortField,
                Descending = descending,
                StatusFilter = status,
                PendingCount = allReviews.Count(r => !r.IsApproved),
                AverageRating = allReviews.Any() ? allReviews.Average(r => r.Rating) : 0,
                TotalPublished = allReviews.Count(r => r.IsApproved),
                ApprovalRate = allReviews.Any() ? (allReviews.Count(r => r.IsApproved) * 100) / allReviews.Count() : 0,
            };

            return this.View(model);
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
