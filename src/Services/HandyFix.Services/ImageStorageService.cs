namespace HandyFix.Services
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    using Microsoft.Extensions.Logging;

    using SkiaSharp;

    // A service's picture. The forty that come with the site are files in wwwroot/images/services,
    // each service's default, and this service never writes there. A picture the admin uploads goes
    // to the photo bucket under a new name each time, "services/{random}_{slug}.webp", with its small
    // copy beside it as "...-320.webp", and the database keeps its address.
    //
    // Until PROJECT_STATE.md Section 3cq an upload was written over the built-in file inside the
    // running container, and a deploy, which starts a new container from what git holds, brought
    // the old picture back; a new service lost its picture, and a rename moved the file to a name
    // the next container did not have. A picture's address no longer holds the service's name, so
    // a rename moves nothing.
    public class ImageStorageService : IImageStorageService
    {
        /// <summary>
        /// The width of a picture's small copy. A category page's card shows the picture 88 pixels
        /// wide on a phone, where the picture itself is 1024 (PROJECT_STATE Section 3cn). The
        /// pictures that come with the site get theirs from tools/image-gen/make-phone-copies.mjs.
        /// </summary>
        public const int SmallCopyWidth = 320;

        private const string BuiltInFolder = "/images/services/";
        private const string BuiltInSuffix = "-hero.webp";
        private const string UploadFolder = "services";

        // The end of an address this service gave an upload: the folder, 32 hex characters, the
        // slug's letters, digits and dashes.
        private static readonly Regex UploadedName = new Regex("/" + UploadFolder + "/[0-9a-f]{32}_[a-z0-9-]+\\.webp$", RegexOptions.CultureInvariant);

        private readonly ICloudflareR2Service r2Service;
        private readonly string builtInDirectory;
        private readonly ILogger<ImageStorageService> logger;
        private readonly string[] allowedExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
        private readonly string[] allowedContentTypes = { "image/jpeg", "image/png", "image/webp" };

        public ImageStorageService(ICloudflareR2Service r2Service, string builtInDirectory, ILogger<ImageStorageService> logger)
        {
            this.r2Service = r2Service;
            this.builtInDirectory = builtInDirectory;
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

            byte[] picture;
            byte[] smallCopy;
            try
            {
                (picture, smallCopy) = await EncodeAsync(stream);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to process service image using SkiaSharp for slug: {Slug}", slug);
                throw;
            }

            // A new name for every upload: a browser that kept the last picture can never show it
            // for this one.
            var name = $"{UploadFolder}/{Guid.NewGuid():N}_{NameFrom(slug)}";

            string pictureUrl;
            using (var pictureStream = new MemoryStream(picture))
            {
                pictureUrl = await this.r2Service.PutFileAsync(pictureStream, name + ".webp", "image/webp");
            }

            try
            {
                using (var copyStream = new MemoryStream(smallCopy))
                {
                    await this.r2Service.PutFileAsync(copyStream, $"{name}-{SmallCopyWidth}.webp", "image/webp");
                }
            }
            catch (Exception ex)
            {
                // The upload has failed as far as the admin is told, so the picture that did go up
                // is taken down again rather than left with nothing pointing at it.
                this.logger.LogError(ex, "Failed to upload the small copy of a service image for slug: {Slug}", slug);
                await this.DeleteQuietlyAsync(pictureUrl);
                throw;
            }

            return pictureUrl;
        }

        public async Task DeleteServiceImageAsync(string imageUrl)
        {
            // A built-in picture is in the site's files, and is never deleted.
            if (!IsUpload(imageUrl))
            {
                return;
            }

            await this.DeleteQuietlyAsync(imageUrl);
            await this.DeleteQuietlyAsync(SmallCopyOf(imageUrl));
        }

        public string GetSmallCopyPublicUrl(string imageUrl)
        {
            // An upload always goes up with its copy.
            if (IsUpload(imageUrl))
            {
                return SmallCopyOf(imageUrl);
            }

            if (imageUrl == null
                || !imageUrl.StartsWith(BuiltInFolder, StringComparison.Ordinal)
                || !imageUrl.EndsWith(BuiltInSuffix, StringComparison.Ordinal))
            {
                return null;
            }

            var slug = imageUrl.Substring(BuiltInFolder.Length, imageUrl.Length - BuiltInFolder.Length - BuiltInSuffix.Length);
            if (string.IsNullOrWhiteSpace(slug) || slug.Contains("..") || slug.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                // Not a name a built-in picture has; the page sends the picture itself.
                return null;
            }

            var copyName = $"{slug}-hero-{SmallCopyWidth}.webp";
            return File.Exists(Path.Combine(this.builtInDirectory, copyName)) ? BuiltInFolder + copyName : null;
        }

        private static bool IsUpload(string imageUrl)
        {
            return imageUrl != null && UploadedName.IsMatch(imageUrl);
        }

        private static string SmallCopyOf(string uploadUrl)
        {
            return uploadUrl.Substring(0, uploadUrl.Length - ".webp".Length) + $"-{SmallCopyWidth}.webp";
        }

        // The slug is in the name only so that the bucket reads sensibly. Kept to what a web address
        // carries as it is, whatever an admin typed into the service's name.
        private static string NameFrom(string slug)
        {
            var name = Regex.Replace((slug ?? string.Empty).ToLowerInvariant(), "[^a-z0-9-]", string.Empty);
            return name.Length == 0 ? "service" : name;
        }

        // The picture, made no wider than 1920, and its small copy, both as WebP.
        private static async Task<(byte[] Picture, byte[] SmallCopy)> EncodeAsync(Stream stream)
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
                        var smallHeight = (int)Math.Round((double)targetBitmap.Height * SmallCopyWidth / targetBitmap.Width);
                        using (var small = targetBitmap.Resize(new SKImageInfo(SmallCopyWidth, smallHeight), SKFilterQuality.High))
                        {
                            return (ToWebp(targetBitmap), ToWebp(small));
                        }
                    }
                    finally
                    {
                        if (isResized)
                        {
                            targetBitmap.Dispose();
                        }
                    }
                }
            }
        }

        private static byte[] ToWebp(SKBitmap bitmap)
        {
            using (var image = SKImage.FromBitmap(bitmap))
            using (SKData data = image.Encode(SKEncodedImageFormat.Webp, 80))
            {
                return data.ToArray();
            }
        }

        // An object left in the bucket costs nothing and harms nobody; a save that failed because
        // of one would. So a delete that fails is logged and the admin's change goes ahead.
        private async Task DeleteQuietlyAsync(string url)
        {
            try
            {
                await this.r2Service.DeleteFileAsync(url);
            }
            catch (Exception ex)
            {
                this.logger.LogWarning(ex, "Could not delete service image {ImageUrl} from the bucket", url);
            }
        }
    }
}
