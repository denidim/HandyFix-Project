namespace HandyFix.Services.Data.Reviews
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using HandyFix.Web.ViewModels.Reviews;

    public interface IReviewsService
    {
        Task AddReviewAsync(string customerName, string comment, int rating, string userId = null);

        Task ApproveReviewAsync(Guid id);

        Task DeleteReviewAsync(Guid id);

        Task<IEnumerable<T>> GetLatestApprovedAsync<T>(int count);

        Task<IEnumerable<T>> GetAllAsync<T>(
            ReviewSortField sortField = ReviewSortField.CreatedOn,
            bool descending = true,
            string statusFilter = null);
    }
}
