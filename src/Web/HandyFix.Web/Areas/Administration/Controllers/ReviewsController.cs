namespace HandyFix.Web.Areas.Administration.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Services.Data.Reviews;
    using HandyFix.Services.Data.ServiceAreas;
    using HandyFix.Web.ViewModels.Reviews;
    using HandyFix.Web.ViewModels.ServiceAreas;

    using Microsoft.AspNetCore.Mvc;

    public class ReviewsController : AdministrationController
    {
        private readonly IReviewsService reviewsService;
        private readonly IServiceAreasService areasService;

        public ReviewsController(IReviewsService reviewsService, IServiceAreasService areasService)
        {
            this.reviewsService = reviewsService;
            this.areasService = areasService;
        }

        public async Task<IActionResult> Index(ReviewSortField sortField = ReviewSortField.CreatedOn, bool descending = true, string status = null)
        {
            var reviews = (await this.reviewsService.GetAllAsync<ReviewViewModel>(sortField, descending, status)).ToList();

            // Summary stats always reflect the whole business, not just whatever status
            // filter is currently applied to the table below.
            List<ReviewViewModel> allReviews = string.IsNullOrWhiteSpace(status)
                ? reviews
                : (await this.reviewsService.GetAllAsync<ReviewViewModel>()).ToList();

            ReviewSummaryStats summary = this.reviewsService.GetSummaryStats(allReviews);

            var model = new ReviewListViewModel
            {
                Reviews = reviews,
                SortField = sortField,
                Descending = descending,
                StatusFilter = status,
                PendingCount = summary.PendingCount,
                AverageRating = summary.AverageRating,
                TotalPublished = summary.TotalPublished,
                ApprovalRate = summary.ApprovalRate,
            };

            return this.View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var model = new ReviewInputModel { ReviewedOn = DateTime.Today };
            await this.AddTownSuggestionsAsync(model);
            return this.View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Create(ReviewInputModel model)
        {
            if (!this.ModelState.IsValid)
            {
                await this.AddTownSuggestionsAsync(model);
                return this.View(model);
            }

            await this.reviewsService.CreateAsync(model);

            this.TempData["SuccessMessage"] = $"{model.CustomerName.Trim()}'s review was added. It is on the website now.";
            return this.RedirectToAction(nameof(this.Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(Guid id)
        {
            ReviewInputModel model = await this.reviewsService.GetByIdAsync<ReviewInputModel>(id);
            if (model == null)
            {
                return this.NotFound();
            }

            await this.AddTownSuggestionsAsync(model);
            return this.View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(ReviewInputModel model)
        {
            if (model.Id == null)
            {
                return this.NotFound();
            }

            if (!this.ModelState.IsValid)
            {
                await this.AddTownSuggestionsAsync(model);
                return this.View(model);
            }

            if (!await this.reviewsService.UpdateAsync(model.Id.Value, model))
            {
                return this.NotFound();
            }

            this.TempData["SuccessMessage"] = $"{model.CustomerName.Trim()}'s review was updated.";
            return this.RedirectToAction(nameof(this.Index));
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

        // The town box offers our area pages' names: a review whose town is written as its page
        // names it shows on that page.
        private async Task AddTownSuggestionsAsync(ReviewInputModel model)
        {
            IEnumerable<ServiceAreaViewModel> areas = await this.areasService.GetAllAsync<ServiceAreaViewModel>(featuredFirst: false);
            model.TownSuggestions = areas.Select(x => x.Name).OrderBy(x => x).ToList();
        }
    }
}
