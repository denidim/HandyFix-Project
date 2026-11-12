namespace HandyFix.Services.Data.Reviews
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using HandyFix.Web.ViewModels.Reviews;

    public interface IReviewsService
    {
        Task ApproveReviewAsync(Guid id);

        Task DeleteReviewAsync(Guid id);

        Task<T> GetByIdAsync<T>(Guid id);

        // A review the admin types in is a Google review already published, so it is approved
        // on saving.
        Task<Guid> CreateAsync(ReviewInputModel input);

        // False when there is no review with that id.
        Task<bool> UpdateAsync(Guid id, ReviewInputModel input);

        // Newest first, by the day each went up on Google.
        Task<IEnumerable<T>> GetLatestApprovedAsync<T>(int count);

        // The same, only the reviews from one town, for that town's area page.
        Task<IEnumerable<T>> GetLatestApprovedInTownAsync<T>(string town, int count);

        Task<IEnumerable<T>> GetAllAsync<T>(
            ReviewSortField sortField = ReviewSortField.CreatedOn,
            bool descending = true,
            string statusFilter = null);

        ReviewSummaryStats GetSummaryStats(IEnumerable<ReviewViewModel> reviews);
    }
}
