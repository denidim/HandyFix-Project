namespace HandyFix.Services.Data.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Data;
    using HandyFix.Data.Models;
    using HandyFix.Data.Repositories;
    using HandyFix.Services;
    using HandyFix.Services.Data.Gallery;
    using HandyFix.Web.ViewModels.Gallery;

    using Microsoft.AspNetCore.Http;
    using Microsoft.EntityFrameworkCore;

    using Moq;

    using Xunit;

    public class GalleryServiceTests
    {
        // New photos go first, in the order they were picked, and the ones already there move down
        // behind them in their own order. They used to go last, so a new photo needed 30 clicks on
        // "Move up" to be seen first (PROJECT_STATE.md Section 3fd).
        [Fact]
        public async Task CreateBatchAsyncShouldUploadPhotosTrimCaptionAndPutThemFirst()
        {
            using ApplicationDbContext dbContext = NewContext();
            using var repository = new EfRepository<GalleryPhoto>(dbContext);
            dbContext.GalleryPhotos.AddRange(
                new GalleryPhoto { ImageUrl = "/images/gallery/job-01.webp", Caption = "Old first", DisplayOrder = 1 },
                new GalleryPhoto { ImageUrl = "/images/gallery/job-02.webp", Caption = "Old second", DisplayOrder = 2 });
            await dbContext.SaveChangesAsync();
            var storage = new Mock<IImageStorageService>();
            int call = 0;
            storage
                .Setup(s => s.SaveGalleryImageAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(() => $"https://photos.example/gallery/{++call:d32}.webp");

            var service = new GalleryService(repository, storage.Object);
            var input = new GalleryBatchUploadInputModel
            {
                Photos = new List<IFormFile>
                {
                    FakeFile("one.jpg", "image/jpeg", 128),
                    FakeFile("two.jpg", "image/jpeg", 256),
                },
                Caption = "  Boiler pressure gauge replaced  ",
                StageBadge = "after",
                ShowOnHomePage = true,
            };

            int saved = await service.CreateBatchAsync(input);

            Assert.Equal(2, saved);
            List<GalleryPhoto> rows = await dbContext.GalleryPhotos.AsNoTracking().OrderBy(p => p.DisplayOrder).ToListAsync();
            Assert.Equal(4, rows.Count);
            Assert.Equal("Boiler pressure gauge replaced", rows[0].Caption);
            Assert.Equal("After", rows[0].StageBadge);
            Assert.True(rows[0].ShowOnHomePage);
            Assert.Equal(
                new[] { (1, $"https://photos.example/gallery/{1:d32}.webp"), (2, $"https://photos.example/gallery/{2:d32}.webp"), (3, "/images/gallery/job-01.webp"), (4, "/images/gallery/job-02.webp") },
                rows.Select(r => (r.DisplayOrder, r.ImageUrl)));
        }

        [Fact]
        public async Task CreateBatchAsyncShouldIgnoreInvalidStageBadgeAndCleanUpIfSecondUploadFails()
        {
            using ApplicationDbContext dbContext = NewContext();
            using var repository = new EfRepository<GalleryPhoto>(dbContext);
            var storage = new Mock<IImageStorageService>();
            const string FirstUrl = "https://photos.example/gallery/00000000000000000000000000000001.webp";

            storage
                .SetupSequence(s => s.SaveGalleryImageAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(FirstUrl)
                .ThrowsAsync(new ImageUploadValidationException("Invalid second photo."));

            var service = new GalleryService(repository, storage.Object);
            var input = new GalleryBatchUploadInputModel
            {
                Photos = new List<IFormFile>
                {
                    FakeFile("one.jpg", "image/jpeg", 128),
                    FakeFile("two.jpg", "image/jpeg", 128),
                },
                Caption = "Pipework tidy",
                StageBadge = "InvalidValue",
            };

            await Assert.ThrowsAsync<ImageUploadValidationException>(() => service.CreateBatchAsync(input));

            storage.Verify(s => s.DeleteGalleryImageAsync(FirstUrl), Times.Once);
            Assert.Empty(dbContext.GalleryPhotos);
        }

        [Fact]
        public async Task GetHomePhotosAsyncShouldReturnOnlyStarredPhotosOrderedAndPopulatedWithCardCopyUrls()
        {
            using ApplicationDbContext dbContext = NewContext();
            using var repository = new EfRepository<GalleryPhoto>(dbContext);

            dbContext.GalleryPhotos.AddRange(
                new GalleryPhoto { ImageUrl = "https://photos.example/gallery/a.webp", Caption = "Second home", ShowOnHomePage = true, DisplayOrder = 2 },
                new GalleryPhoto { ImageUrl = "https://photos.example/gallery/b.webp", Caption = "Gallery only", ShowOnHomePage = false, DisplayOrder = 1 },
                new GalleryPhoto { ImageUrl = "https://photos.example/gallery/c.webp", Caption = "First home", ShowOnHomePage = true, DisplayOrder = 1 });
            await dbContext.SaveChangesAsync();

            var storage = new Mock<IImageStorageService>();
            storage
                .Setup(s => s.GetGalleryCardCopyPublicUrl(It.IsAny<string>()))
                .Returns<string>(url => url.Replace(".webp", "-640.webp"));

            var service = new GalleryService(repository, storage.Object);
            IReadOnlyList<GalleryPhotoViewModel> home = await service.GetHomePhotosAsync();

            Assert.Equal(new[] { "First home", "Second home" }, home.Select(p => p.Caption));
            Assert.Equal("https://photos.example/gallery/c-640.webp", home[0].CardImageUrl);
        }

        [Fact]
        public async Task MoveAsyncShouldSwapAdjacentPhotosAndRenumber()
        {
            using ApplicationDbContext dbContext = NewContext();
            using var repository = new EfRepository<GalleryPhoto>(dbContext);

            var first = new GalleryPhoto { ImageUrl = "https://photos.example/gallery/1.webp", Caption = "One", DisplayOrder = 1 };
            var second = new GalleryPhoto { ImageUrl = "https://photos.example/gallery/2.webp", Caption = "Two", DisplayOrder = 1 };
            dbContext.GalleryPhotos.AddRange(first, second);
            await dbContext.SaveChangesAsync();

            var service = new GalleryService(repository, new Mock<IImageStorageService>().Object);
            IReadOnlyList<GalleryPhotoViewModel> initial = await service.GetAllAsync();
            Guid secondInList = initial[1].Id;

            bool moved = await service.MoveAsync(secondInList, -1);

            Assert.True(moved);
            IReadOnlyList<GalleryPhotoViewModel> afterMove = await service.GetAllAsync();
            Assert.Equal(secondInList, afterMove[0].Id);
            Assert.Equal(1, afterMove[0].DisplayOrder);
            Assert.Equal(2, afterMove[1].DisplayOrder);
        }

        [Fact]
        public async Task ToggleUpdateAndDeleteShouldModifyAndRemovePhotoAndBucketCopy()
        {
            using ApplicationDbContext dbContext = NewContext();
            using var repository = new EfRepository<GalleryPhoto>(dbContext);
            const string ImageUrl = "https://photos.example/gallery/0123456789abcdef0123456789abcdef.webp";

            var entity = new GalleryPhoto { ImageUrl = ImageUrl, Caption = "Initial", ShowOnHomePage = false, DisplayOrder = 1 };
            dbContext.GalleryPhotos.Add(entity);
            await dbContext.SaveChangesAsync();

            var storage = new Mock<IImageStorageService>();
            var service = new GalleryService(repository, storage.Object);

            bool? starred = await service.ToggleHomePageAsync(entity.Id);
            Assert.True(starred);

            bool updated = await service.UpdateAsync(entity.Id, new GalleryPhotoEditInputModel
            {
                Caption = "  Updated caption ",
                StageBadge = "Before",
                ShowOnHomePage = true,
                DisplayOrder = 5,
            });
            Assert.True(updated);

            GalleryPhoto saved = await dbContext.GalleryPhotos.SingleAsync(p => p.Id == entity.Id);
            Assert.Equal("Updated caption", saved.Caption);
            Assert.Equal("Before", saved.StageBadge);
            Assert.Equal(5, saved.DisplayOrder);

            bool deleted = await service.DeleteAsync(entity.Id);
            Assert.True(deleted);
            Assert.Empty(dbContext.GalleryPhotos);
            storage.Verify(s => s.DeleteGalleryImageAsync(ImageUrl), Times.Once);
        }

        private static ApplicationDbContext NewContext() =>
            new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);

        private static IFormFile FakeFile(string fileName, string contentType, long length)
        {
            var file = new Mock<IFormFile>();
            file.SetupGet(f => f.FileName).Returns(fileName);
            file.SetupGet(f => f.ContentType).Returns(contentType);
            file.SetupGet(f => f.Length).Returns(length);
            file.Setup(f => f.OpenReadStream()).Returns(() => new MemoryStream(new byte[] { 1, 2, 3, 4 }));
            return file.Object;
        }
    }
}
