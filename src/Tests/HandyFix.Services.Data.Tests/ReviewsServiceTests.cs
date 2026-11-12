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

        // A review the admin types in is a Google review already published: it is approved on
        // saving, so it shows at once (PROJECT_STATE.md Section 3eh).
        [Fact]
        public async Task CreateAsyncShouldSaveAnApprovedReviewWithItsTownJobAndGoogleDate()
        {
            using var dbContext = NewContext();
            using var repository = new EfDeletableEntityRepository<Review>(dbContext);
            var service = new ReviewsService(repository);

            Guid id = await service.CreateAsync(new ReviewInputModel
            {
                CustomerName = "  Sarah K. ",
                Town = " Chessington ",
                JobDone = "Boiler service",
                Rating = 5,
                Comment = " Quick, tidy and friendly. ",
                ReviewedOn = new DateTime(2026, 11, 6, 15, 30, 0),
            });

            Review saved = dbContext.Reviews.Single(x => x.Id == id);
            Assert.True(saved.IsApproved);
            Assert.Equal("Sarah K.", saved.CustomerName);
            Assert.Equal("Chessington", saved.Town);
            Assert.Equal("Boiler service", saved.JobDone);
            Assert.Equal("Quick, tidy and friendly.", saved.Comment);
            Assert.Equal(new DateTime(2026, 11, 6), saved.ReviewedOn);
        }

        [Fact]
        public async Task UpdateAsyncShouldChangeTheReviewAndSayWhenThereIsNone()
        {
            using var dbContext = NewContext();
            using var repository = new EfDeletableEntityRepository<Review>(dbContext);
            var review = new Review { CustomerName = "Sarah K.", Town = "Esher", Rating = 4, Comment = "Good work.", IsApproved = true };
            dbContext.Reviews.Add(review);
            await dbContext.SaveChangesAsync();
            var service = new ReviewsService(repository);

            var input = new ReviewInputModel
            {
                CustomerName = "Sarah K.",
                Town = " ",
                Rating = 5,
                Comment = "Good work, done in an hour.",
                ReviewedOn = new DateTime(2026, 11, 7),
            };

            Assert.True(await service.UpdateAsync(review.Id, input));
            Assert.False(await service.UpdateAsync(Guid.NewGuid(), input));

            Review updated = dbContext.Reviews.Single(x => x.Id == review.Id);
            Assert.Null(updated.Town);
            Assert.Equal(5, updated.Rating);
            Assert.Equal("Good work, done in an hour.", updated.Comment);
            Assert.Equal(new DateTime(2026, 11, 7), updated.ReviewedOn);
        }

        // Six reviews typed in on one day keep the order they went up on Google in; a review saved
        // before the Google date existed falls back to its own date.
        [Fact]
        public async Task GetLatestApprovedAsyncShouldPutTheNewestOnGoogleFirst()
        {
            using var dbContext = NewContext();
            using var repository = new EfDeletableEntityRepository<Review>(dbContext);
            dbContext.Reviews.AddRange(
                new Review { CustomerName = "Older", Comment = "Older review.", Rating = 5, IsApproved = true, ReviewedOn = new DateTime(2026, 11, 1) },
                new Review { CustomerName = "Newer", Comment = "Newer review.", Rating = 5, IsApproved = true, ReviewedOn = new DateTime(2026, 11, 9) },
                new Review { CustomerName = "Hidden", Comment = "Not approved.", Rating = 5, IsApproved = false, ReviewedOn = new DateTime(2026, 11, 10) },
                new Review { CustomerName = "Undated", Comment = "Saved long ago.", Rating = 5, IsApproved = true, CreatedOn = new DateTime(2026, 10, 1) });
            await dbContext.SaveChangesAsync();
            var service = new ReviewsService(repository);

            var names = (await service.GetLatestApprovedAsync<ReviewViewModel>(10)).Select(x => x.CustomerName).ToList();

            Assert.Equal(new[] { "Newer", "Older", "Undated" }, names);
        }

        [Fact]
        public async Task GetLatestApprovedInTownAsyncShouldGiveOnlyThatTownsApprovedReviews()
        {
            using var dbContext = NewContext();
            using var repository = new EfDeletableEntityRepository<Review>(dbContext);
            dbContext.Reviews.AddRange(
                new Review { CustomerName = "Esher One", Town = "Esher", Comment = "From Esher.", Rating = 5, IsApproved = true },
                new Review { CustomerName = "Esher Pending", Town = "Esher", Comment = "Not approved.", Rating = 5, IsApproved = false },
                new Review { CustomerName = "Cobham One", Town = "Cobham", Comment = "From Cobham.", Rating = 5, IsApproved = true },
                new Review { CustomerName = "No Town", Comment = "Town not known.", Rating = 5, IsApproved = true });
            await dbContext.SaveChangesAsync();
            var service = new ReviewsService(repository);

            var esher = (await service.GetLatestApprovedInTownAsync<ReviewViewModel>("esher ", 3)).Select(x => x.CustomerName).ToList();
            var none = await service.GetLatestApprovedInTownAsync<ReviewViewModel>(null, 3);

            Assert.Equal(new[] { "Esher One" }, esher);
            Assert.Empty(none);
        }

        private static ApplicationDbContext NewContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;
            return new ApplicationDbContext(options);
        }
    }
}
