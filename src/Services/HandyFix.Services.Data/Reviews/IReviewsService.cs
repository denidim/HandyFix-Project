namespace HandyFix.Services.Data.Reviews
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public interface IReviewsService
    {
        Task AddReviewAsync(string customerName, string comment, int rating, string userId = null);

        Task ApproveReviewAsync(Guid id);

        Task DeleteReviewAsync(Guid id);

        Task<IEnumerable<T>> GetLatestApprovedAsync<T>(int count);

        Task<IEnumerable<T>> GetAllAsync<T>();
    }
}
