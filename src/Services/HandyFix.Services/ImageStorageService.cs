namespace HandyFix.Services
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;

    using Microsoft.Extensions.Logging;

    using SkiaSharp;

    public class ImageStorageService : IImageStorageService
    {
        /// <summary>
        /// The width of a picture's small copy, written beside it as <c>{slug}-hero-320.webp</c>.
        /// A category page's card shows the picture 88 pixels wide on a phone, where the picture
        /// itself is 1024 (PROJECT_STATE Section 3cn). The pictures that come with the site get
        /// theirs from tools/image-gen/make-phone-copies.mjs.
        /// </summary>
        public const int SmallCopyWidth = 320;

        private const string PublicFolder = "/images/services/";
        private const string PictureSuffix = "-hero.webp";

        private readonly string targetDirectory;
        private readonly ILogger<ImageStorageService> logger;
        private readonly string[] allowedExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
        private readonly string[] allowedContentTypes = { "image/jpeg", "image/png", "image/webp" };
        private readonly string[] legacyExtensions = { ".jpg", ".jpeg", ".png" };

        public ImageStorageService(string targetDirectory, ILogger<ImageStorageService> logger)
        {
            this.targetDirectory = targetDirectory;
            this.logger = logger;
        }

        public async Task<string> SaveServiceImageAsync(Stream stream, string fileName, string contentType, string slug)
        {
            if (stream == null || stream.Length == 0)
            {
                return null;
            }

            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            if (!this.allowedExtensions.Contains(extension) || !this.allowedContentTypes.Contains(contentType.ToLowerInvariant()))
            {
                throw new InvalidOperationException("Unsupported file type. Only JPEG, PNG, and WEBP images are allowed.");
            }

            try
            {
                using (var memoryStream = new MemoryStream())
                {
                    await stream.CopyToAsync(memoryStream);
                    memoryStream.Position = 0;

                    using (var originalBitmap = SKBitmap.Decode(memoryStream))
                    {
                        if (originalBitmap == null)
                        {
                            throw new InvalidOperationException("Failed to decode uploaded image.");
                        }

                        SKBitmap targetBitmap = originalBitmap;
                        var isResized = false;

                        if (originalBitmap.Width > 1920)
                        {
                            var targetWidth = 1920;
                            var targetHeight = (int)Math.Round((double)originalBitmap.Height * targetWidth / originalBitmap.Width);
                            targetBitmap = originalBitmap.Resize(new SKImageInfo(targetWidth, targetHeight), SKFilterQuality.High);
                            isResized = true;
                        }

                        try
                        {
                            using (var image = SKImage.FromBitmap(targetBitmap))
                            using (SKData data = image.Encode(SKEncodedImageFormat.Webp, 80))
                            {
                                var filePath = this.GetServiceImagePath(slug);
                                var directory = Path.GetDirectoryName(filePath);
                                if (!Directory.Exists(directory))
                                {
                                    Directory.CreateDirectory(directory);
                                }

                                using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
                                {
                                    data.SaveTo(fileStream);
                                }
                            }

                            // Written every time, so the copy is never an older picture than the
                            // one it stands for: the new picture takes the same file name.
                            this.SaveSmallCopy(targetBitmap, slug);
                            this.DeleteLegacyImages(slug);
                        }
                        finally
                        {
                            if (isResized && targetBitmap != null)
                            {
                                targetBitmap.Dispose();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to process and save service image using SkiaSharp for slug: {Slug}", slug);
                throw;
            }

            return this.GetServiceImagePublicUrl(slug);
        }

        public void DeleteServiceImage(string slug)
        {
            try
            {
                foreach (var filePath in new[] { this.GetServiceImagePath(slug), this.GetSmallCopyPath(slug) })
                {
                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                    }
                }

                this.DeleteLegacyImages(slug);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to delete service image for slug: {Slug}", slug);
            }
        }

        public void RenameServiceImage(string oldSlug, string newSlug)
        {
            try
            {
                var oldPath = this.GetServiceImagePath(oldSlug);
                var newPath = this.GetServiceImagePath(newSlug);

                if (File.Exists(oldPath))
                {
                    var directory = Path.GetDirectoryName(newPath);
                    if (!Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    if (File.Exists(newPath))
                    {
                        File.Delete(newPath);
                    }

                    File.Move(oldPath, newPath);

                    // The small copy goes with it. One already under the new name belongs to an
                    // earlier picture and would stand for this one, so it goes either way.
                    var oldCopy = this.GetSmallCopyPath(oldSlug);
                    var newCopy = this.GetSmallCopyPath(newSlug);
                    if (File.Exists(newCopy))
                    {
                        File.Delete(newCopy);
                    }

                    if (File.Exists(oldCopy))
                    {
                        File.Move(oldCopy, newCopy);
                    }
                }

                this.DeleteLegacyImages(oldSlug);
                this.DeleteLegacyImages(newSlug);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to rename service image from {OldSlug} to {NewSlug}", oldSlug, newSlug);
            }
        }

        public bool ServiceImageExists(string slug)
        {
            try
            {
                var filePath = this.GetServiceImagePath(slug);
                if (File.Exists(filePath))
                {
                    return true;
                }

                return this.legacyExtensions.Any(ext => File.Exists(Path.Combine(this.targetDirectory, $"{slug}-hero{ext}")));
            }
            catch (Exception ex)
            {
                this.logger.LogWarning(ex, "Error checking existence of service image for slug: {Slug}", slug);
                return false;
            }
        }

        public string GetServiceImagePublicUrl(string slug)
        {
            return $"{PublicFolder}{slug}{PictureSuffix}";
        }

        public string GetSmallCopyPublicUrl(string imageUrl)
        {
            if (imageUrl == null
                || !imageUrl.StartsWith(PublicFolder, StringComparison.Ordinal)
                || !imageUrl.EndsWith(PictureSuffix, StringComparison.Ordinal))
            {
                return null;
            }

            var slug = imageUrl.Substring(PublicFolder.Length, imageUrl.Length - PublicFolder.Length - PictureSuffix.Length);
            try
            {
                return File.Exists(this.GetSmallCopyPath(slug)) ? $"{PublicFolder}{slug}-hero-{SmallCopyWidth}.webp" : null;
            }
            catch (ArgumentException)
            {
                // Not a name this service would have written; the page sends the picture itself.
                return null;
            }
        }

        private string GetServiceImagePath(string slug)
        {
            if (string.IsNullOrWhiteSpace(slug) || slug.Contains("..") || slug.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                throw new ArgumentException("Invalid or unsafe service slug.");
            }

            return Path.Combine(this.targetDirectory, $"{slug}{PictureSuffix}");
        }

        private string GetSmallCopyPath(string slug)
        {
            // The same checks on the slug as the picture's own path.
            return Path.ChangeExtension(this.GetServiceImagePath(slug), null) + $"-{SmallCopyWidth}.webp";
        }

        private void SaveSmallCopy(SKBitmap picture, string slug)
        {
            var height = (int)Math.Round((double)picture.Height * SmallCopyWidth / picture.Width);
            using (var small = picture.Resize(new SKImageInfo(SmallCopyWidth, height), SKFilterQuality.High))
            using (var image = SKImage.FromBitmap(small))
            using (SKData data = image.Encode(SKEncodedImageFormat.Webp, 80))
            using (var fileStream = new FileStream(this.GetSmallCopyPath(slug), FileMode.Create, FileAccess.Write))
            {
                data.SaveTo(fileStream);
            }
        }

        private void DeleteLegacyImages(string slug)
        {
            foreach (var ext in this.legacyExtensions)
            {
                var legacyPath = Path.Combine(this.targetDirectory, $"{slug}-hero{ext}");
                if (File.Exists(legacyPath))
                {
                    try
                    {
                        File.Delete(legacyPath);
                    }
                    catch (Exception ex)
                    {
                        this.logger.LogWarning(ex, "Could not delete legacy file {LegacyPath}", legacyPath);
                    }
                }
            }
        }
    }
}
