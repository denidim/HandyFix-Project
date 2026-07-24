namespace HandyFix.Services.Data.Reviews
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Data.Common.Repositories;
    using HandyFix.Data.Models;
    using HandyFix.Services.Mapping;
    using HandyFix.Web.ViewModels.Reviews;

    using Microsoft.EntityFrameworkCore;

    public class ReviewsService : IReviewsService
    {
        private readonly IDeletableEntityRepository<Review> reviewRepository;

        public ReviewsService(IDeletableEntityRepository<Review> reviewRepository)
        {
            this.reviewRepository = reviewRepository;
        }

        public async Task AddReviewAsync(string customerName, string comment, int rating, string userId = null)
        {
            var review = new Review
            {
                CustomerName = customerName,
                Comment = comment,
                Rating = rating,
                UserId = userId,
                IsApproved = false,
            };

            await this.reviewRepository.AddAsync(review);
            await this.reviewRepository.SaveChangesAsync();
        }

        public async Task ApproveReviewAsync(Guid id)
        {
            var review = await this.reviewRepository.AllWithDeleted().FirstOrDefaultAsync(x => x.Id == id);
            if (review != null)
            {
                review.IsApproved = true;
                await this.reviewRepository.SaveChangesAsync();
            }
        }

        public async Task DeleteReviewAsync(Guid id)
        {
            var review = await this.reviewRepository.All().FirstOrDefaultAsync(x => x.Id == id);
            if (review != null)
            {
                this.reviewRepository.Delete(review);
                await this.reviewRepository.SaveChangesAsync();
            }
        }

        public async Task<IEnumerable<T>> GetLatestApprovedAsync<T>(int count)
        {
            return await this.reviewRepository.All()
                .Where(x => x.IsApproved)
                .OrderByDescending(x => x.CreatedOn)
                .Take(count)
                .To<T>()
                .ToListAsync();
        }

        public async Task<IEnumerable<T>> GetAllAsync<T>(
            ReviewSortField sortField = ReviewSortField.CreatedOn,
            bool descending = true,
            string statusFilter = null)
        {
            var query = this.reviewRepository.AllWithDeleted();

            if (string.Equals(statusFilter, "Approved", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(x => x.IsApproved);
            }
            else if (string.Equals(statusFilter, "Pending", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(x => !x.IsApproved);
            }

            query = sortField switch
            {
                ReviewSortField.CustomerName => descending
                    ? query.OrderByDescending(x => x.CustomerName)
                    : query.OrderBy(x => x.CustomerName),
                ReviewSortField.Rating => descending
                    ? query.OrderByDescending(x => x.Rating)
                    : query.OrderBy(x => x.Rating),
                _ => descending
                    ? query.OrderByDescending(x => x.CreatedOn)
                    : query.OrderBy(x => x.CreatedOn),
            };

            return await query.To<T>().ToListAsync();
        }
    }
}
