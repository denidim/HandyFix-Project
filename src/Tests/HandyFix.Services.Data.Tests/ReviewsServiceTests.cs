namespace HandyFix.Services.Data.Tests
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Data;
    using HandyFix.Data.Models;
    using HandyFix.Data.Repositories;
    using HandyFix.Services.Data.Reviews;

    using Microsoft.EntityFrameworkCore;

    using Xunit;

    public class ReviewsServiceTests
    {
        [Fact]
        public async Task AddReviewAsyncShouldAddUnapprovedReview()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;
            
            using var dbContext = new ApplicationDbContext(options);
            using var repository = new EfDeletableEntityRepository<Review>(dbContext);

            var service = new ReviewsService(repository);
            await service.AddReviewAsync("Alice Smith", "Amazing plumbing repairs!", 5);

            Assert.Equal(1, dbContext.Reviews.Count());
            var review = dbContext.Reviews.First();
            Assert.Equal("Alice Smith", review.CustomerName);
            Assert.Equal(5, review.Rating);
            Assert.Equal("Amazing plumbing repairs!", review.Comment);
            Assert.False(review.IsApproved);
        }

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
    }
}
