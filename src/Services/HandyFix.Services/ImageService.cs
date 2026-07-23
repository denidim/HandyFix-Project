namespace HandyFix.Services
{
    using System;
    using System.Collections.Generic;
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
                throw new InvalidOperationException(
                    $"A maximum of {MaxFileCount} images can be uploaded at once.");
            }

            var urls = new List<string>(files.Count);

            foreach (var file in files)
            {
                if (file.Length > MaxFileSizeBytes)
                {
                    throw new InvalidOperationException(
                        $"File \"{file.FileName}\" exceeds the maximum allowed size of {MaxFileSizeBytes / (1024 * 1024)} MB.");
                }

                var contentType = file.ContentType?.ToLowerInvariant();
                if (string.IsNullOrEmpty(contentType) || !AllowedContentTypes.Contains(contentType))
                {
                    throw new InvalidOperationException(
                        $"File \"{file.FileName}\" has an unsupported type \"{file.ContentType}\". Allowed types: JPEG, PNG, WEBP.");
                }

                try
                {
                    using (var stream = file.OpenReadStream())
                    {
                        var url = await this.r2Service.UploadFileAsync(stream, file.FileName, contentType, folder);
                        urls.Add(url);
                    }
                }
                catch (Exception ex)
                {
                    this.logger.LogError(ex, "Failed to upload image {FileName} to folder {Folder}", file.FileName, folder);
                    throw;
                }
            }

            return urls;
        }
    }
}
