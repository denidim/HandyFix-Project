namespace HandyFix.Services.Data.Tests
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    using HandyFix.Data;
    using HandyFix.Data.Seeding;

    using Microsoft.Data.Sqlite;
    using Microsoft.EntityFrameworkCore;

    using Xunit;

    // Seeded into an empty table and left untouched thereafter so admin edits and deletions persist.
    public sealed class GallerySeederTests : IDisposable
    {
        private readonly SqliteConnection connection;
        private readonly ApplicationDbContext dbContext;

        public GallerySeederTests()
        {
            this.connection = new SqliteConnection("DataSource=:memory:");
            this.connection.Open();

            this.dbContext = new ApplicationDbContext(
                new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(this.connection).Options);
            this.dbContext.Database.EnsureCreated();
        }

        public void Dispose()
        {
            this.dbContext.Dispose();
            this.connection.Dispose();
        }

        [Fact]
        public async Task SeedAsyncShouldPopulateAllThirtyBuiltInGalleryPhotosWithValidFilesAndMetadata()
        {
            await this.SeedAsync();

            var photos = await this.dbContext.GalleryPhotos
                .OrderBy(p => p.DisplayOrder)
                .ToListAsync();

            Assert.Equal(30, photos.Count);
            Assert.Equal(12, photos.Count(p => p.ShowOnHomePage));
            Assert.Equal(18, photos.Count(p => !p.ShowOnHomePage));
            Assert.Equal(Enumerable.Range(1, 30), photos.Select(p => p.DisplayOrder));

            var webRoot = FindWebRootPath();
            var urlPattern = new Regex("^/images/gallery/job-[0-9]{2}\\.webp$");

            Assert.All(photos, photo =>
            {
                Assert.Matches(urlPattern, photo.ImageUrl);
                Assert.False(string.IsNullOrWhiteSpace(photo.Caption));
                Assert.True(photo.Caption.Length <= 90, $"Caption too long: {photo.Caption}");
                Assert.True(
                    photo.StageBadge == null || photo.StageBadge == "Before" || photo.StageBadge == "After",
                    $"Unexpected stage badge '{photo.StageBadge}' on {photo.ImageUrl}");

                var relativePath = photo.ImageUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
                var fullPath = Path.Combine(webRoot, relativePath);
                var cardPath = fullPath[..^".webp".Length] + "-640.webp";

                Assert.True(File.Exists(fullPath), $"Missing gallery photo {fullPath}");
                Assert.True(File.Exists(cardPath), $"Missing gallery card copy {cardPath}");
            });
        }

        [Fact]
        public async Task SeedAsyncShouldNotResurrectRemovedPhotoWhenRunAgain()
        {
            await this.SeedAsync();

            var first = await this.dbContext.GalleryPhotos
                .OrderBy(p => p.DisplayOrder)
                .FirstAsync();
            var removedUrl = first.ImageUrl;

            this.dbContext.GalleryPhotos.Remove(first);
            await this.dbContext.SaveChangesAsync();

            await this.SeedAsync();

            var photos = await this.dbContext.GalleryPhotos.ToListAsync();
            Assert.Equal(29, photos.Count);
            Assert.DoesNotContain(photos, p => p.ImageUrl == removedUrl);
        }

        private static string FindWebRootPath()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                var candidate = Path.Combine(directory.FullName, "src", "Web", "HandyFix.Web", "wwwroot");
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }

                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException("Could not locate src/Web/HandyFix.Web/wwwroot from test output directory.");
        }

        private async Task SeedAsync()
        {
            await new GallerySeeder().SeedAsync(this.dbContext, null);
            await this.dbContext.SaveChangesAsync();
        }
    }
}
