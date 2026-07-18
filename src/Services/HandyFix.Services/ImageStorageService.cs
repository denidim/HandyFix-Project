namespace HandyFix.Services
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;

    using Microsoft.Extensions.Logging;

    public class ImageStorageService : IImageStorageService
    {
        private readonly string targetDirectory;
        private readonly ILogger<ImageStorageService> logger;
        private readonly string[] allowedExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
        private readonly string[] allowedContentTypes = { "image/jpeg", "image/png", "image/webp" };

        public ImageStorageService(string targetDirectory, ILogger<ImageStorageService> logger)
        {
            this.targetDirectory = targetDirectory;
            this.logger = logger;
        }

        public async Task<bool> SaveServiceImageAsync(Stream stream, string fileName, string contentType, string slug)
        {
            if (stream == null || stream.Length == 0)
            {
                return false;
            }

            // MIME type & extension validation
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            if (!this.allowedExtensions.Contains(extension) || !this.allowedContentTypes.Contains(contentType.ToLowerInvariant()))
            {
                throw new InvalidOperationException("Unsupported file type. Only JPEG, PNG, and WEBP images are allowed.");
            }

            var filePath = this.GetServiceImagePath(slug);
            var directory = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            try
            {
                // Save file
                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await stream.CopyToAsync(fileStream);
                }
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to save service image for slug: {Slug}", slug);
                throw;
            }

            return true;
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
                return File.Exists(filePath);
            }
            catch (Exception ex)
            {
                this.logger.LogWarning(ex, "Error checking existence of service image for slug: {Slug}", slug);
                return false;
            }
        }

        private string GetServiceImagePath(string slug)
        {
            // Sanitize slug to prevent path traversal
            if (string.IsNullOrWhiteSpace(slug) || slug.Contains("..") || slug.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                throw new ArgumentException("Invalid or unsafe service slug.");
            }

            return Path.Combine(this.targetDirectory, $"{slug}-hero.jpg");
        }
    }
}
