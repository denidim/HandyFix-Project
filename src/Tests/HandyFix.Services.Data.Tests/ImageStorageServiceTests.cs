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

        // A small copy is drawn from the picture's mipmaps, as SkiaSharp 2 drew it, so thin detail
        // becomes a light shade. The cubic filter SkiaSharp 4 offers in place of the old "high
        // quality" setting looks at too few pixels when a picture shrinks ten times: these lines
        // vanished, and a photo's small copy came out grainy (PROJECT_STATE.md Section 3dk).
        [Fact]
        public async Task ASmallCopyKeepsThinLinesAsALightShade()
        {
            using var bitmap = new SKBitmap(3200, 2400);
            using (var canvas = new SKCanvas(bitmap))
            using (var black = new SKPaint { Color = SKColors.Black, IsAntialias = false })
            {
                canvas.Clear(SKColors.White);
                for (var x = 0; x < bitmap.Width; x += 10)
                {
                    canvas.DrawRect(x, 0, 1, bitmap.Height, black);
                }
            }

            using var image = SKImage.FromBitmap(bitmap);
            using SKData png = image.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = new MemoryStream(png.ToArray());
            var url = await this.storage.SaveServiceImageAsync(stream, "lines.png", "image/png", "lines");

            var key = url.Substring(FakeBucket.PublicUrl.Length + 1).Replace(".webp", "-320.webp");
            using var copy = SKBitmap.Decode(this.bucket.Objects[key]);
            var brightness = copy.Pixels.Average(p => (p.Red + p.Green + p.Blue) / 3.0);

            // One pixel in ten is black: about 230 when the lines are kept, 255 when they are lost.
            Assert.InRange(brightness, 215, 245);
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

        // A file the site does not take is the admin's to put right. It is refused with its own
        // exception type and words written for the admin, which the form shows as they are;
        // anything else that fails here is told in the form's own words.
        [Fact]
        public async Task AFileThatIsNotAJpegPngOrWebpIsRefusedInWordsForTheAdmin()
        {
            using var stream = new MemoryStream(new byte[] { 1, 2, 3, 4 });

            ImageUploadValidationException refusal = await Assert.ThrowsAsync<ImageUploadValidationException>(
                () => this.storage.SaveServiceImageAsync(stream, "notes.pdf", "application/pdf", "tap-repairs"));

            Assert.Equal("Unsupported file type. Only JPEG, PNG, and WEBP images are allowed.", refusal.Message);
            Assert.Empty(this.bucket.Objects);
        }

        [Fact]
        public async Task AFileThatIsNotReallyAPictureIsRefusedInWordsForTheAdmin()
        {
            using var stream = new MemoryStream(new byte[] { 1, 2, 3, 4 });

            ImageUploadValidationException refusal = await Assert.ThrowsAsync<ImageUploadValidationException>(
                () => this.storage.SaveServiceImageAsync(stream, "photo.png", "image/png", "tap-repairs"));

            Assert.Equal("Failed to decode uploaded image.", refusal.Message);
            Assert.Empty(this.bucket.Objects);
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

        // An area's picture is the strip across the top of its page, 1600 x 700, and a phone is
        // sent the middle 928 of it; the middle of whatever is uploaded is kept, as the page
        // draws it (PROJECT_STATE.md Section 3dl). Here only the middle 1600 of a 4000-wide
        // picture is green, so a picture cut anywhere else shows red at its edges.
        [Fact]
        public async Task AnAreaUploadIsTheMiddleOfThePictureAt1600By700WithAPhoneStrip928Wide()
        {
            using var bitmap = new SKBitmap(4000, 700);
            using (var canvas = new SKCanvas(bitmap))
            using (var green = new SKPaint { Color = SKColors.Lime })
            {
                canvas.Clear(SKColors.Red);
                canvas.DrawRect(1200, 0, 1600, 700, green);
            }

            var url = await this.UploadAreaAsync(bitmap, "cobham");

            Assert.Matches("^https://photos\\.example/areas/[0-9a-f]{32}_cobham\\.webp$", url);
            var key = url.Substring(FakeBucket.PublicUrl.Length + 1);
            var phoneKey = key.Replace(".webp", "-phone.webp");
            Assert.Equal(new[] { key, phoneKey }, this.bucket.Objects.Keys.OrderBy(x => x.Length));
            Assert.Equal($"{FakeBucket.PublicUrl}/{phoneKey}", this.storage.GetAreaPhoneCopyPublicUrl(url));

            using var picture = SKBitmap.Decode(this.bucket.Objects[key]);
            Assert.Equal((1600, 700), (picture.Width, picture.Height));
            AssertGreen(picture.GetPixel(4, 350));
            AssertGreen(picture.GetPixel(1595, 350));

            using var phone = SKBitmap.Decode(this.bucket.Objects[phoneKey]);
            Assert.Equal((928, 700), (phone.Width, phone.Height));
            Assert.Empty(Directory.GetFiles(this.builtInFolder));
        }

        // A small or tall picture is drawn larger to fill the strip rather than refused: the
        // admin can always upload a better one.
        [Fact]
        public async Task ASmallTallAreaUploadStillFillsTheStrip()
        {
            using var bitmap = new SKBitmap(600, 900);
            bitmap.Erase(SKColors.Lime);

            var url = await this.UploadAreaAsync(bitmap, "esher");

            using var picture = SKBitmap.Decode(this.bucket.Objects[url.Substring(FakeBucket.PublicUrl.Length + 1)]);
            Assert.Equal((1600, 700), (picture.Width, picture.Height));
            AssertGreen(picture.GetPixel(2, 2));
        }

        [Fact]
        public async Task DeletingAnAreaUploadDeletesItsPhoneCopy()
        {
            using var bitmap = new SKBitmap(1600, 700);
            bitmap.Erase(SKColors.Lime);
            var url = await this.UploadAreaAsync(bitmap, "cobham");

            await this.storage.DeleteAreaImageAsync(url);

            Assert.Empty(this.bucket.Objects);
        }

        // Only an address this service gave an area's upload is ever deleted as one: not a
        // service's picture, nor one that came with the site.
        [Theory]
        [InlineData("/images/areas/cobham-hero.webp")]
        [InlineData("https://photos.example/services/0123456789abcdef0123456789abcdef_tap.webp")]
        [InlineData(null)]
        public async Task AnAddressThatIsNotAnAreaUploadIsNeverDeletedAsOne(string imageUrl)
        {
            await this.storage.DeleteAreaImageAsync(imageUrl);

            Assert.Empty(this.bucket.Deleted);
            Assert.Null(this.storage.GetAreaPhoneCopyPublicUrl(imageUrl));
        }

        [Fact]
        public async Task AnAreaFileThatIsNotAJpegPngOrWebpIsRefusedInWordsForTheAdmin()
        {
            using var stream = new MemoryStream(new byte[] { 1, 2, 3, 4 });

            ImageUploadValidationException refusal = await Assert.ThrowsAsync<ImageUploadValidationException>(
                () => this.storage.SaveAreaImageAsync(stream, "notes.pdf", "application/pdf", "cobham"));

            Assert.Equal("Unsupported file type. Only JPEG, PNG, and WEBP images are allowed.", refusal.Message);
            Assert.Empty(this.bucket.Objects);
        }

        public void Dispose()
        {
            Directory.Delete(this.builtInFolder, recursive: true);
        }

        // WebP at quality 80 shifts a colour a little; green stays green and red stays red.
        private static void AssertGreen(SKColor pixel)
        {
            Assert.True(pixel.Green > 200 && pixel.Red < 60, $"Expected green, got {pixel}.");
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

        private async Task<string> UploadAreaAsync(SKBitmap bitmap, string slug)
        {
            using var image = SKImage.FromBitmap(bitmap);
            using SKData png = image.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = new MemoryStream(png.ToArray());

            return await this.storage.SaveAreaImageAsync(stream, "photo.png", "image/png", slug);
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
