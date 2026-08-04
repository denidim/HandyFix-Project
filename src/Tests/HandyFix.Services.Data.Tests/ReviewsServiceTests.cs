namespace HandyFix.Services.Data.Tests
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Data;
    using HandyFix.Data.Models;
    using HandyFix.Data.Repositories;
    using HandyFix.Services.Data.Reviews;
    using HandyFix.Web.ViewModels.Reviews;

    using Microsoft.EntityFrameworkCore;

    using Xunit;

    public class ReviewsServiceTests
    {
        [Fact]
        public async Task ApproveReviewAsyncShouldMarkAsApproved()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;
            
            using var dbContext = new ApplicationDbContext(options);
            using var repository = new EfDeletableEntityRepository<Review>(dbContext);

            var review = new Review { CustomerName = "Bob Jones", Rating = 4, Comment = "Very prompt handyman", IsApproved = false };
            dbContext.Reviews.Add(review);
            await dbContext.SaveChangesAsync();

            var service = new ReviewsService(repository);
            await service.ApproveReviewAsync(review.Id);

            var updated = dbContext.Reviews.First(x => x.Id == review.Id);
            Assert.True(updated.IsApproved);
        }

        [Fact]
        public async Task DeleteReviewAsyncShouldRemoveReviewFromGetAllAsyncResults()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;

            using var dbContext = new ApplicationDbContext(options);
            using var repository = new EfDeletableEntityRepository<Review>(dbContext);

            var review = new Review { CustomerName = "Test Reviewer", Rating = 5, Comment = "Delete me please." };
            dbContext.Reviews.Add(review);
            await dbContext.SaveChangesAsync();

            var service = new ReviewsService(repository);
            await service.DeleteReviewAsync(review.Id);

            var results = (await service.GetAllAsync<ReviewViewModel>()).ToList();
            Assert.Empty(results);
        }

        [Fact]
        public async Task GetAllAsyncShouldSortByRatingDescendingWhenRequested()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;

            using var dbContext = new ApplicationDbContext(options);
            using var repository = new EfDeletableEntityRepository<Review>(dbContext);

            dbContext.Reviews.Add(new Review { CustomerName = "Low Rater", Comment = "It was okay overall.", Rating = 2 });
            dbContext.Reviews.Add(new Review { CustomerName = "High Rater", Comment = "Fantastic service, would recommend.", Rating = 5 });
            await dbContext.SaveChangesAsync();

            var service = new ReviewsService(repository);
            var results = (await service.GetAllAsync<ReviewViewModel>(ReviewSortField.Rating, descending: true)).ToList();

            Assert.Equal("High Rater", results.First().CustomerName);
            Assert.Equal("Low Rater", results.Last().CustomerName);
        }

        [Fact]
        public async Task GetAllAsyncShouldFilterByApprovalStatusWhenRequested()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;

            using var dbContext = new ApplicationDbContext(options);
            using var repository = new EfDeletableEntityRepository<Review>(dbContext);

            var approved = new Review { CustomerName = "Approved Reviewer", Comment = "Great work, very tidy.", Rating = 5, IsApproved = true };
            var pending = new Review { CustomerName = "Pending Reviewer", Comment = "Still waiting for moderation.", Rating = 3, IsApproved = false };
            dbContext.Reviews.AddRange(approved, pending);
            await dbContext.SaveChangesAsync();

            var service = new ReviewsService(repository);

            var approvedResults = (await service.GetAllAsync<ReviewViewModel>(statusFilter: "Approved")).ToList();
            Assert.Single(approvedResults);
            Assert.Equal(approved.Id, approvedResults.Single().Id);

            var pendingResults = (await service.GetAllAsync<ReviewViewModel>(statusFilter: "Pending")).ToList();
            Assert.Single(pendingResults);
            Assert.Equal(pending.Id, pendingResults.Single().Id);
        }

        [Fact]
        public void GetSummaryStatsShouldDescribeTheGivenListRegardlessOfWhereItCameFrom()
        {
            // A pure computation over whatever list it's handed - no repository access needed.
            var service = new ReviewsService(null);

            var reviews = new[]
            {
                new ReviewViewModel { Rating = 5, IsApproved = true },
                new ReviewViewModel { Rating = 3, IsApproved = true },
                new ReviewViewModel { Rating = 1, IsApproved = false },
            };

            ReviewSummaryStats stats = service.GetSummaryStats(reviews);

            Assert.Equal(1, stats.PendingCount);
            Assert.Equal(3, stats.TotalPublished + stats.PendingCount);
            Assert.Equal(2, stats.TotalPublished);
            Assert.Equal(3, stats.AverageRating);
            Assert.Equal(66, stats.ApprovalRate);
        }

        [Fact]
        public void GetSummaryStatsShouldReturnZeroesForAnEmptyList()
        {
            var service = new ReviewsService(null);

            ReviewSummaryStats stats = service.GetSummaryStats(Enumerable.Empty<ReviewViewModel>());

            Assert.Equal(0, stats.PendingCount);
            Assert.Equal(0, stats.TotalPublished);
            Assert.Equal(0, stats.AverageRating);
            Assert.Equal(0, stats.ApprovalRate);
        }
    }
}
