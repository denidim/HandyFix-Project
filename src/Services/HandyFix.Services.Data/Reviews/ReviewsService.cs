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

        public async Task ApproveReviewAsync(Guid id)
        {
            Review review = await this.reviewRepository.All().FirstOrDefaultAsync(x => x.Id == id);
            if (review != null)
            {
                review.IsApproved = true;
                await this.reviewRepository.SaveChangesAsync();
            }
        }

        public async Task DeleteReviewAsync(Guid id)
        {
            Review review = await this.reviewRepository.All().FirstOrDefaultAsync(x => x.Id == id);
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
            IQueryable<Review> query = this.reviewRepository.All();

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

        public async Task<int> GetPendingCountAsync()
        {
            // AllWithDeleted() to match the Dashboard's existing count exactly - not changing
            // that behavior here, just relocating it out of the controller.
            return await this.reviewRepository.AllWithDeleted().CountAsync(x => !x.IsApproved);
        }

        public ReviewSummaryStats GetSummaryStats(IEnumerable<ReviewViewModel> reviews)
        {
            // Deliberately takes the list rather than fetching it - see the equivalent note on
            // IBookingsService.GetSummaryStats: the caller decides whether it already has the
            // right (unfiltered) list in hand or needs to fetch one.
            List<ReviewViewModel> reviewList = reviews.ToList();

            return new ReviewSummaryStats
            {
                PendingCount = reviewList.Count(r => !r.IsApproved),
                AverageRating = reviewList.Any() ? reviewList.Average(r => r.Rating) : 0,
                TotalPublished = reviewList.Count(r => r.IsApproved),
                ApprovalRate = reviewList.Any() ? (reviewList.Count(r => r.IsApproved) * 100) / reviewList.Count : 0,
            };
        }
    }
}
