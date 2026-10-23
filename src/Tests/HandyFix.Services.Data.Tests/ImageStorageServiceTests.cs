namespace HandyFix.Services.Data.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    using HandyFix.Services;

    using Microsoft.Extensions.Logging.Abstractions;

    using SkiaSharp;

    using Xunit;

    // A service picture the admin uploads goes to the photo bucket, never into the site's own
    // files, which a deploy replaces with what git holds (PROJECT_STATE.md Section 3cq). Each
    // upload has a new name and a small copy beside it, which a category page's card sends to a
    // phone (Section 3cn). The pictures that come with the site stay where they are, each
    // service's default, and are never deleted.
    public sealed class ImageStorageServiceTests : IDisposable
    {
        private readonly string builtInFolder = Path.Combine(Path.GetTempPath(), "handyfix-images-" + Guid.NewGuid().ToString("N"));
        private readonly FakeBucket bucket = new FakeBucket();
        private readonly ImageStorageService storage;

        public ImageStorageServiceTests()
        {
            Directory.CreateDirectory(this.builtInFolder);
            this.storage = new ImageStorageService(this.bucket, this.builtInFolder, NullLogger<ImageStorageService>.Instance);
        }

        [Fact]
        public async Task AnUploadGoesToTheBucketWithACopy320WideAndNothingIntoTheSitesFiles()
        {
            var url = await this.UploadAsync("tap-repairs", SKColors.Red);

            Assert.Matches("^https://photos\\.example/services/[0-9a-f]{32}_tap-repairs\\.webp$", url);
            var key = url.Substring(FakeBucket.PublicUrl.Length + 1);
            Assert.Equal(new[] { key, key.Replace(".webp", "-320.webp") }, this.bucket.Objects.Keys.OrderBy(x => x.Length));
            Assert.All(this.bucket.ContentTypes.Values, type => Assert.Equal("image/webp", type));

            using var copy = SKBitmap.Decode(this.bucket.Objects[key.Replace(".webp", "-320.webp")]);
            Assert.Equal(320, copy.Width);
            Assert.Equal(240, copy.Height);
            Assert.Equal(url.Replace(".webp", "-320.webp"), this.storage.GetSmallCopyPublicUrl(url));

            Assert.Empty(Directory.GetFiles(this.builtInFolder));
        }

        // A browser that kept the last picture can never show it for the new one.
        [Fact]
        public async Task EachUploadHasANewName()
        {
            var first = await this.UploadAsync("tap-repairs", SKColors.Red);
            var second = await this.UploadAsync("tap-repairs", SKColors.Blue);

            Assert.NotEqual(first, second);
        }

        // Only letters, digits and dashes of a slug go into a name, so the address works as written.
        [Fact]
        public async Task AnUnusualSlugStillGivesAPlainName()
        {
            var url = await this.UploadAsync("tap-#1?-repairs", SKColors.Red);

            Assert.Matches("/services/[0-9a-f]{32}_tap-1-repairs\\.webp$", url);
        }

        [Fact]
        public async Task DeletingAnUploadDeletesItsCopy()
        {
            var url = await this.UploadAsync("tap-repairs", SKColors.Red);

            await this.storage.DeleteServiceImageAsync(url);

            Assert.Empty(this.bucket.Objects);
        }

        // A built-in picture used to be deleted with its service, until the next deploy put it back.
        [Fact]
        public async Task ABuiltInPictureIsNeverDeleted()
        {
            File.WriteAllText(this.PathOf("tap-repairs-hero.webp"), "the picture that came with the site");

            await this.storage.DeleteServiceImageAsync("/images/services/tap-repairs-hero.webp");

            Assert.True(File.Exists(this.PathOf("tap-repairs-hero.webp")));
            Assert.Empty(this.bucket.Deleted);
        }

        // An upload whose copy cannot go up has failed for the admin, so the picture is taken down
        // again rather than left in the bucket with nothing pointing at it.
        [Fact]
        public async Task AnUploadWhoseCopyFailsLeavesNothingBehind()
        {
            this.bucket.FailOnPut = 2;

            await Assert.ThrowsAsync<InvalidOperationException>(() => this.UploadAsync("tap-repairs", SKColors.Red));

            Assert.Empty(this.bucket.Objects);
        }

        // A bucket that cannot be reached must not stop the admin deleting a service or replacing
        // its picture; what is left there costs nothing.
        [Fact]
        public async Task ADeleteTheBucketRefusesIsLoggedNotThrown()
        {
            var url = await this.UploadAsync("tap-repairs", SKColors.Red);
            this.bucket.FailOnDelete = true;

            await this.storage.DeleteServiceImageAsync(url);

            Assert.Equal(2, this.bucket.Objects.Count);
        }

        [Fact]
        public void ABuiltInPictureWithACopyBesideItOffersTheCopy()
        {
            File.WriteAllText(this.PathOf("tap-repairs-hero.webp"), "picture");
            File.WriteAllText(this.PathOf("tap-repairs-hero-320.webp"), "copy");

            Assert.Equal("/images/services/tap-repairs-hero-320.webp", this.storage.GetSmallCopyPublicUrl("/images/services/tap-repairs-hero.webp"));
        }

        // A built-in picture with no copy beside it, and an address that is not a service
        // picture's at all, are sent as they are.
        [Theory]
        [InlineData("/images/services/tap-repairs-hero.webp")]
        [InlineData("/images/hero.webp")]
        [InlineData("https://photos.example/services/tap-repairs-hero.webp")]
        [InlineData("https://photos.example/inquiries/0123456789abcdef0123456789abcdef_tap.webp")]
        [InlineData("/images/services/../hero-hero.webp")]
        [InlineData(null)]
        public void AnAddressWithoutACopyGetsNone(string imageUrl)
        {
            File.WriteAllText(this.PathOf("tap-repairs-hero.webp"), "a picture with no copy");

            Assert.Null(this.storage.GetSmallCopyPublicUrl(imageUrl));
        }

        [Fact]
        public void ABuiltInPictureWithAMiddleCopyBesideItOffersThatCopy()
        {
            File.WriteAllText(this.PathOf("tap-repairs-hero.webp"), "picture");
            File.WriteAllText(this.PathOf("tap-repairs-hero-640.webp"), "copy");

            Assert.Equal("/images/services/tap-repairs-hero-640.webp", this.storage.GetMediumCopyPublicUrl("/images/services/tap-repairs-hero.webp"));
        }

        // Uploads made before the middle copy existed have none, so no upload is offered one: a
        // page that asked for a copy the bucket does not hold would show a broken picture.
        [Fact]
        public async Task AnUploadIsNeverOfferedAMiddleCopy()
        {
            var url = await this.UploadAsync("tap-repairs", SKColors.Red);

            Assert.Null(this.storage.GetMediumCopyPublicUrl(url));
        }

        [Theory]
        [InlineData("/images/services/tap-repairs-hero.webp")]
        [InlineData("/images/hero.webp")]
        [InlineData("/images/services/../hero-hero.webp")]
        [InlineData(null)]
        public void AnAddressWithoutAMiddleCopyGetsNone(string imageUrl)
        {
            File.WriteAllText(this.PathOf("tap-repairs-hero.webp"), "a picture with no copy");
            File.WriteAllText(this.PathOf("tap-repairs-hero-320.webp"), "only the small copy");

            Assert.Null(this.storage.GetMediumCopyPublicUrl(imageUrl));
        }

        public void Dispose()
        {
            Directory.Delete(this.builtInFolder, recursive: true);
        }

        private string PathOf(string fileName) => Path.Combine(this.builtInFolder, fileName);

        // A 1024 x 768 picture in one colour, sent as a PNG the way a browser sends an upload.
        private async Task<string> UploadAsync(string slug, SKColor colour)
        {
            using var bitmap = new SKBitmap(1024, 768);
            bitmap.Erase(colour);
            using var image = SKImage.FromBitmap(bitmap);
            using SKData png = image.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = new MemoryStream(png.ToArray());

            return await this.storage.SaveServiceImageAsync(stream, "photo.png", "image/png", slug);
        }

        // The photo bucket, kept in memory: what was put in it under which key, and what was asked
        // to leave it.
        private sealed class FakeBucket : ICloudflareR2Service
        {
            public const string PublicUrl = "https://photos.example";

            private int puts;

            public Dictionary<string, byte[]> Objects { get; } = new Dictionary<string, byte[]>();

            public Dictionary<string, string> ContentTypes { get; } = new Dictionary<string, string>();

            public List<string> Deleted { get; } = new List<string>();

            // The put, counted from 1, that fails; 0 for none.
            public int FailOnPut { get; set; }

            public bool FailOnDelete { get; set; }

            public Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType, string folder)
            {
                throw new NotSupportedException("Service pictures name their own keys.");
            }

            public async Task<string> PutFileAsync(Stream fileStream, string key, string contentType)
            {
                if (++this.puts == this.FailOnPut)
                {
                    throw new InvalidOperationException("The bucket could not be reached.");
                }

                using var copy = new MemoryStream();
                await fileStream.CopyToAsync(copy);
                this.Objects[key] = copy.ToArray();
                this.ContentTypes[key] = contentType;
                return $"{PublicUrl}/{key}";
            }

            public Task DeleteFileAsync(string fileUrl)
            {
                if (this.FailOnDelete)
                {
                    throw new InvalidOperationException("The bucket could not be reached.");
                }

                var key = CloudflareR2Service.GetObjectKey(PublicUrl, fileUrl);
                this.Deleted.Add(key);
                this.Objects.Remove(key);
                return Task.CompletedTask;
            }
        }
    }
}
