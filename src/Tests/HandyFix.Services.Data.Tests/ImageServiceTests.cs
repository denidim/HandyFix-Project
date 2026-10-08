namespace HandyFix.Services.Data.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Services;

    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Logging.Abstractions;

    using Moq;

    using Xunit;

    // The photos a visitor attaches to an enquiry or a booking (PROJECT_STATE.md Section 3cb).
    public class ImageServiceTests
    {
        // A photo the site does not take is the visitor's to put right, and says so with its own
        // exception type. The forms show its message; anything else that goes wrong is storage,
        // and is kept from the visitor.
        [Theory]
        [InlineData("image/heic", 1024, "unsupported type")]
        [InlineData("application/pdf", 1024, "unsupported type")]
        [InlineData("image/jpeg", (16L * 1024 * 1024) + 1, "exceeds the maximum allowed size")]
        public async Task UploadImagesAsyncShouldRefuseAPhotoItDoesNotTakeWithAMessageForTheVisitor(string contentType, long bytes, string expected)
        {
            var storage = new Mock<ICloudflareR2Service>();
            var service = new ImageService(storage.Object, NullLogger<ImageService>.Instance);

            ImageUploadValidationException refusal = await Assert.ThrowsAsync<ImageUploadValidationException>(
                () => service.UploadImagesAsync(new[] { Photo("tap.jpg", "image/jpeg", 1024), Photo("other.bin", contentType, bytes) }, "inquiries"));

            Assert.Contains(expected, refusal.Message);

            // Every photo is checked before any goes up. Checked as they were uploaded, the good
            // first photo was left in storage with nothing pointing at it.
            storage.Verify(x => x.UploadFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task UploadImagesAsyncShouldRefuseMoreThanFivePhotos()
        {
            var storage = new Mock<ICloudflareR2Service>();
            var service = new ImageService(storage.Object, NullLogger<ImageService>.Instance);
            IEnumerable<IFormFile> six = Enumerable.Range(1, 6).Select(i => Photo($"photo{i}.jpg", "image/jpeg", 1024));

            await Assert.ThrowsAsync<ImageUploadValidationException>(() => service.UploadImagesAsync(six, "bookings"));
        }

        [Fact]
        public async Task UploadImagesAsyncShouldReturnTheAddressOfEachPhotoAndSkipEmptyOnes()
        {
            var storage = new Mock<ICloudflareR2Service>();
            storage
                .Setup(x => x.UploadFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), "inquiries"))
                .ReturnsAsync((Stream stream, string fileName, string contentType, string folder) => $"https://photos.example/{folder}/{fileName}");
            var service = new ImageService(storage.Object, NullLogger<ImageService>.Instance);

            IReadOnlyList<string> urls = await service.UploadImagesAsync(
                new[] { Photo("a.jpg", "image/jpeg", 10), Photo("empty.jpg", "image/jpeg", 0), Photo("b.png", "IMAGE/PNG", 10) },
                "inquiries");

            Assert.Equal(new[] { "https://photos.example/inquiries/a.jpg", "https://photos.example/inquiries/b.png" }, urls);
        }

        // Storage fails on the second photo. The caller is told the upload failed and saves no
        // photo at all, so the first one is taken down again and not left behind.
        [Fact]
        public async Task UploadImagesAsyncShouldTakeDownWhatWentUpWhenStorageFailsPartWay()
        {
            var storage = new Mock<ICloudflareR2Service>();
            storage
                .Setup(x => x.UploadFileAsync(It.IsAny<Stream>(), "a.jpg", It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync("https://photos.example/inquiries/a.jpg");
            storage
                .Setup(x => x.UploadFileAsync(It.IsAny<Stream>(), "b.jpg", It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(new IOException("Storage is unreachable."));
            var service = new ImageService(storage.Object, NullLogger<ImageService>.Instance);

            // Not the visitor's kind of failure: it is not an ImageUploadValidationException.
            await Assert.ThrowsAsync<IOException>(
                () => service.UploadImagesAsync(new[] { Photo("a.jpg", "image/jpeg", 10), Photo("b.jpg", "image/jpeg", 10) }, "inquiries"));

            storage.Verify(x => x.DeleteFileAsync("https://photos.example/inquiries/a.jpg"), Times.Once);
        }

        [Fact]
        public async Task DeleteImagesAsyncShouldRemoveEachPhotoAndStopAtTheFirstThatCannotBeRemoved()
        {
            var storage = new Mock<ICloudflareR2Service>();
            storage.Setup(x => x.DeleteFileAsync("https://photos.example/b.jpg")).ThrowsAsync(new IOException("Storage is unreachable."));
            var service = new ImageService(storage.Object, NullLogger<ImageService>.Instance);

            await Assert.ThrowsAsync<IOException>(
                () => service.DeleteImagesAsync(new[] { "https://photos.example/a.jpg", "https://photos.example/b.jpg", "https://photos.example/c.jpg" }));

            storage.Verify(x => x.DeleteFileAsync("https://photos.example/a.jpg"), Times.Once);
            storage.Verify(x => x.DeleteFileAsync("https://photos.example/c.jpg"), Times.Never);
        }

        private static IFormFile Photo(string fileName, string contentType, long bytes)
        {
            var photo = new Mock<IFormFile>();
            photo.SetupGet(f => f.FileName).Returns(fileName);
            photo.SetupGet(f => f.ContentType).Returns(contentType);
            photo.SetupGet(f => f.Length).Returns(bytes);
            photo.Setup(f => f.OpenReadStream()).Returns(() => new MemoryStream(new byte[] { 1, 2, 3 }));
            return photo.Object;
        }
    }
}
