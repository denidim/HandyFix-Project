namespace HandyFix.Services.Data.Tests
{
    using System;
    using System.IO;
    using System.Threading.Tasks;

    using HandyFix.Services;

    using Microsoft.Extensions.Logging.Abstractions;

    using SkiaSharp;

    using Xunit;

    // A service picture is written with a small copy beside it, which a category page's card sends
    // to a phone (PROJECT_STATE.md Section 3cn). The copy must never stand for an older picture:
    // an upload takes the same file name, so the copy is written again with it, and it goes with
    // the picture when the picture is deleted or its service renamed.
    public sealed class ImageStorageServiceTests : IDisposable
    {
        private readonly string folder = Path.Combine(Path.GetTempPath(), "handyfix-images-" + Guid.NewGuid().ToString("N"));
        private readonly ImageStorageService storage;

        public ImageStorageServiceTests()
        {
            this.storage = new ImageStorageService(this.folder, NullLogger<ImageStorageService>.Instance);
        }

        [Fact]
        public async Task AnUploadIsWrittenWithACopy320Wide()
        {
            await this.UploadAsync("tap-repairs", SKColors.Red);

            using var copy = SKBitmap.Decode(this.PathOf("tap-repairs-hero-320.webp"));
            Assert.Equal(320, copy.Width);
            Assert.Equal(240, copy.Height);
            Assert.Equal("/images/services/tap-repairs-hero-320.webp", this.storage.GetSmallCopyPublicUrl("/images/services/tap-repairs-hero.webp"));
        }

        [Fact]
        public async Task ANewPictureReplacesTheCopyAsWellAsThePicture()
        {
            await this.UploadAsync("tap-repairs", SKColors.Red);
            await this.UploadAsync("tap-repairs", SKColors.Blue);

            using var copy = SKBitmap.Decode(this.PathOf("tap-repairs-hero-320.webp"));
            SKColor middle = copy.GetPixel(160, 120);
            Assert.True(middle.Blue > 200 && middle.Red < 50, $"the copy is still the old picture: {middle}");
        }

        [Fact]
        public async Task DeletingThePictureDeletesItsCopy()
        {
            await this.UploadAsync("tap-repairs", SKColors.Red);

            this.storage.DeleteServiceImage("tap-repairs");

            Assert.False(File.Exists(this.PathOf("tap-repairs-hero.webp")));
            Assert.False(File.Exists(this.PathOf("tap-repairs-hero-320.webp")));
            Assert.Null(this.storage.GetSmallCopyPublicUrl("/images/services/tap-repairs-hero.webp"));
        }

        [Fact]
        public async Task RenamingAServiceTakesTheCopyAlongAndDropsAnOldOneUnderTheNewName()
        {
            await this.UploadAsync("tap-repairs", SKColors.Red);
            File.WriteAllText(this.PathOf("tap-fixing-hero-320.webp"), "a copy left by an earlier picture");

            this.storage.RenameServiceImage("tap-repairs", "tap-fixing");

            Assert.True(File.Exists(this.PathOf("tap-fixing-hero.webp")));
            using var copy = SKBitmap.Decode(this.PathOf("tap-fixing-hero-320.webp"));
            Assert.Equal(320, copy.Width);
            Assert.False(File.Exists(this.PathOf("tap-repairs-hero-320.webp")));
        }

        // A picture with no copy beside it (one saved before copies were made), and an address
        // that is not a service picture's at all, are sent as they are.
        [Theory]
        [InlineData("/images/services/tap-repairs-hero.webp")]
        [InlineData("/images/hero.webp")]
        [InlineData("https://photos.example/services/tap-repairs-hero.webp")]
        [InlineData("/images/services/../hero-hero.webp")]
        [InlineData(null)]
        public void AnAddressWithoutACopyGetsNone(string imageUrl)
        {
            Directory.CreateDirectory(this.folder);
            File.WriteAllText(this.PathOf("tap-repairs-hero.webp"), "a picture saved before copies were made");

            Assert.Null(this.storage.GetSmallCopyPublicUrl(imageUrl));
        }

        public void Dispose()
        {
            if (Directory.Exists(this.folder))
            {
                Directory.Delete(this.folder, recursive: true);
            }
        }

        private string PathOf(string fileName) => Path.Combine(this.folder, fileName);

        // A 1024 x 768 picture in one colour, sent as a PNG the way a browser sends an upload.
        private async Task UploadAsync(string slug, SKColor colour)
        {
            using var bitmap = new SKBitmap(1024, 768);
            bitmap.Erase(colour);
            using var image = SKImage.FromBitmap(bitmap);
            using SKData png = image.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = new MemoryStream(png.ToArray());

            await this.storage.SaveServiceImageAsync(stream, "photo.png", "image/png", slug);
        }
    }
}
