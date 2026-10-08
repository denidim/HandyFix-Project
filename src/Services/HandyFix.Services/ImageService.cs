namespace HandyFix.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;

    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Logging;

    public class ImageService : IImageService
    {
        private const int MaxFileCount = 5;
        private const long MaxFileSizeBytes = 15L * 1024 * 1024;

        private static readonly string[] AllowedContentTypes =
        {
            "image/jpeg",
            "image/png",
            "image/webp",
        };

        private readonly ICloudflareR2Service r2Service;
        private readonly ILogger<ImageService> logger;

        public ImageService(
            ICloudflareR2Service r2Service,
            ILogger<ImageService> logger)
        {
            this.r2Service = r2Service;
            this.logger = logger;
        }

        public async Task<IReadOnlyList<string>> UploadImagesAsync(IEnumerable<IFormFile> images, string folder)
        {
            if (images == null)
            {
                return Array.Empty<string>();
            }

            var files = images.Where(f => f.Length > 0).ToList();

            if (files.Count == 0)
            {
                return Array.Empty<string>();
            }

            if (files.Count > MaxFileCount)
            {
                throw new ImageUploadValidationException(
                    $"A maximum of {MaxFileCount} images can be uploaded at once.");
            }

            // Every file is checked before any is uploaded. Checked one by one as they went up, a
            // bad third file left the first two in storage with nothing pointing at them.
            foreach (IFormFile file in files)
            {
                if (file.Length > MaxFileSizeBytes)
                {
                    throw new ImageUploadValidationException(
                        $"File \"{file.FileName}\" exceeds the maximum allowed size of {MaxFileSizeBytes / (1024 * 1024)} MB.");
                }

                var contentType = file.ContentType?.ToLowerInvariant();
                if (string.IsNullOrEmpty(contentType) || !AllowedContentTypes.Contains(contentType))
                {
                    throw new ImageUploadValidationException(
                        $"File \"{file.FileName}\" has an unsupported type \"{file.ContentType}\". Allowed types: JPEG, PNG, WEBP.");
                }
            }

            var urls = new List<string>(files.Count);

            foreach (IFormFile file in files)
            {
                try
                {
                    using (Stream stream = file.OpenReadStream())
                    {
                        var url = await this.r2Service.UploadFileAsync(stream, file.FileName, file.ContentType.ToLowerInvariant(), folder);
                        urls.Add(url);
                    }
                }
                catch (Exception ex)
                {
                    this.logger.LogError(ex, "Failed to upload image {FileName} to folder {Folder}", file.FileName, folder);

                    // The caller hears that the upload failed and saves nothing for these photos,
                    // so the ones that did go up are taken down again, as far as that is possible.
                    await this.RemoveQuietlyAsync(urls);
                    throw;
                }
            }

            return urls;
        }

        public async Task DeleteImagesAsync(IEnumerable<string> imageUrls)
        {
            if (imageUrls == null)
            {
                return;
            }

            foreach (var url in imageUrls)
            {
                try
                {
                    await this.r2Service.DeleteFileAsync(url);
                }
                catch (Exception ex)
                {
                    this.logger.LogError(ex, "Failed to delete image {ImageUrl}", url);
                    throw;
                }
            }
        }

        private async Task RemoveQuietlyAsync(IEnumerable<string> imageUrls)
        {
            foreach (var url in imageUrls)
            {
                try
                {
                    await this.r2Service.DeleteFileAsync(url);
                }
                catch (Exception ex)
                {
                    this.logger.LogWarning(ex, "Could not remove image {ImageUrl} after a failed upload", url);
                }
            }
        }
    }
}
