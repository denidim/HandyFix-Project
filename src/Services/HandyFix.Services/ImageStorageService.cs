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
                            using (var data = image.Encode(SKEncodedImageFormat.Webp, 80))
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

            return $"/images/services/{slug}-hero.webp";
        }

        public void DeleteServiceImage(string slug)
        {
            try
            {
                var filePath = this.GetServiceImagePath(slug);
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
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

        public void ConvertExistingJpgServiceImages()
        {
            if (!Directory.Exists(this.targetDirectory))
            {
                return;
            }

            var jpgFiles = Directory.GetFiles(this.targetDirectory, "*.jpg");
            foreach (var jpgPath in jpgFiles)
            {
                try
                {
                    var fileName = Path.GetFileNameWithoutExtension(jpgPath);
                    var slug = fileName.EndsWith("-hero") ? fileName.Substring(0, fileName.Length - 5) : fileName;
                    var webpPath = Path.Combine(this.targetDirectory, $"{slug}-hero.webp");

                    if (!File.Exists(webpPath))
                    {
                        using (var memoryStream = new MemoryStream())
                        {
                            using (var fileStream = File.OpenRead(jpgPath))
                            {
                                fileStream.CopyTo(memoryStream);
                            }

                            memoryStream.Position = 0;

                            using (var originalBitmap = SKBitmap.Decode(memoryStream))
                            {
                                if (originalBitmap != null)
                                {
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
                                        using (var data = image.Encode(SKEncodedImageFormat.Webp, 80))
                                        using (var outStream = new FileStream(webpPath, FileMode.Create, FileAccess.Write))
                                        {
                                            data.SaveTo(outStream);
                                        }
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
                    }

                    File.Delete(jpgPath);
                }
                catch (Exception ex)
                {
                    this.logger.LogWarning(ex, "Failed to convert legacy image {JpgPath} to WebP", jpgPath);
                }
            }
        }

        private string GetServiceImagePath(string slug)
        {
            if (string.IsNullOrWhiteSpace(slug) || slug.Contains("..") || slug.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                throw new ArgumentException("Invalid or unsafe service slug.");
            }

            return Path.Combine(this.targetDirectory, $"{slug}-hero.webp");
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
