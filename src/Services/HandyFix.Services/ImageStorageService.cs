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
    //
    // An area's picture works the same way since Section 3dl: an upload goes to
    // "areas/{random}_{slug}.webp" at 1600 x 700, with its phone copy as "...-phone.webp". The 15
    // that came with the site stay in wwwroot/images/areas.
    public class ImageStorageService : IImageStorageService
    {
        /// <summary>
        /// The width of a picture's small copy. A category page's card shows the picture 88 pixels
        /// wide on a phone, where the picture itself is 1024 (PROJECT_STATE Section 3cn). The
        /// pictures that come with the site get theirs from tools/image-gen/make-phone-copies.mjs.
        /// </summary>
        public const int SmallCopyWidth = 320;

        /// <summary>
        /// The width of a built-in picture's middle copy. A service card on an area, pricing or
        /// service page shows the picture about 340 pixels wide on a phone and never more than 422
        /// on a computer, where the picture is 1024 (PROJECT_STATE Section 3cz). The copies come
        /// from tools/image-gen/make-phone-copies.mjs; an upload has none, and is sent whole.
        /// </summary>
        public const int MediumCopyWidth = 640;

        /// <summary>
        /// The size of an area's picture: the strip across the top of its page, as the 15 that
        /// came with the site were made.
        /// </summary>
        public const int AreaWidth = 1600;

        /// <summary>The height of an area's picture.</summary>
        public const int AreaHeight = 700;

        /// <summary>
        /// The width of an area picture's phone copy, a strip from its middle as tall as the
        /// picture. A phone draws the picture to fill a box 400px tall and shows only its middle:
        /// 928 of its 1600 pixels at the widest phone (PROJECT_STATE Section 3cz). The pictures
        /// that came with the site get theirs from tools/image-gen/make-phone-copies.mjs.
        /// </summary>
        public const int AreaPhoneWidth = 928;

        public const int GalleryMaxDimension = 1600;

        public const int GalleryCardWidth = 640;

        public const int GalleryCardHeight = 480;

        private const string BuiltInFolder = "/images/services/";
        private const string BuiltInSuffix = "-hero.webp";
        private const string UploadFolder = "services";
        private const string AreaFolder = "areas";
        private const string AreaPhoneSuffix = "-phone.webp";
        private const string GalleryFolder = "gallery";
        private const string GalleryCardSuffix = "-640.webp";

        // The end of an address this service gave an upload: the folder, 32 hex characters, the
        // slug's letters, digits and dashes.
        private static readonly Regex UploadedName = new Regex("/" + UploadFolder + "/[0-9a-f]{32}_[a-z0-9-]+\\.webp$", RegexOptions.CultureInvariant);

        // The same for an area's picture.
        private static readonly Regex UploadedAreaName = new Regex("/" + AreaFolder + "/[0-9a-f]{32}_[a-z0-9-]+\\.webp$", RegexOptions.CultureInvariant);

        // The same for a gallery photo.
        private static readonly Regex UploadedGalleryName = new Regex("/" + GalleryFolder + "/[0-9a-f]{32}\\.webp$", RegexOptions.CultureInvariant);

        // Built-in gallery photos live in wwwroot/images/gallery with a -640 card copy beside each.
        private static readonly Regex BuiltInGalleryName = new Regex("^/images/gallery/job-[0-9]{2}\\.webp$", RegexOptions.CultureInvariant);

        // How a picture is made smaller. SkiaSharp 4 dropped the "high quality" setting used until
        // then. When shrinking, SkiaSharp 2 quietly drew a high-quality picture from its mipmaps
        // (copies of the picture at half, a quarter, ...) with a linear filter, and that is what
        // this asks for. The cubic filter the setting's name suggests left the small copies grainy
        // and a third bigger; this one makes them as before (PROJECT_STATE.md Section 3dk).
        private static readonly SKSamplingOptions ResizeQuality = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Nearest);

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

            this.CheckFileType(fileName, contentType);

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
            var name = $"{UploadFolder}/{Guid.NewGuid():N}_{NameFrom(slug, "service")}";

            return await this.PutWithCopyAsync(picture, name + ".webp", smallCopy, $"{name}-{SmallCopyWidth}.webp", slug);
        }

        public async Task<string> SaveAreaImageAsync(Stream stream, string fileName, string contentType, string slug)
        {
            if (stream == null || stream.Length == 0)
            {
                return null;
            }

            this.CheckFileType(fileName, contentType);

            byte[] picture;
            byte[] phoneCopy;
            try
            {
                (picture, phoneCopy) = await EncodeAreaAsync(stream);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to process area image using SkiaSharp for slug: {Slug}", slug);
                throw;
            }

            var name = $"{AreaFolder}/{Guid.NewGuid():N}_{NameFrom(slug, "area")}";

            return await this.PutWithCopyAsync(picture, name + ".webp", phoneCopy, name + AreaPhoneSuffix, slug);
        }

        public async Task DeleteAreaImageAsync(string imageUrl)
        {
            if (!IsAreaUpload(imageUrl))
            {
                return;
            }

            await this.DeleteQuietlyAsync(imageUrl);
            await this.DeleteQuietlyAsync(AreaPhoneCopyOf(imageUrl));
        }

        public string GetAreaPhoneCopyPublicUrl(string imageUrl)
        {
            // An upload always goes up with its phone copy.
            return IsAreaUpload(imageUrl) ? AreaPhoneCopyOf(imageUrl) : null;
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

            return this.BuiltInCopy(imageUrl, SmallCopyWidth);
        }

        public string GetMediumCopyPublicUrl(string imageUrl)
        {
            // Uploads made before this copy existed have none, and the bucket cannot be asked
            // cheaply, so an upload is sent whole, as it always was.
            return IsUpload(imageUrl) ? null : this.BuiltInCopy(imageUrl, MediumCopyWidth);
        }

        public async Task<string> SaveGalleryImageAsync(Stream stream, string fileName, string contentType)
        {
            if (stream == null || stream.Length == 0)
            {
                return null;
            }

            this.CheckFileType(fileName, contentType);

            byte[] picture;
            byte[] cardCopy;
            try
            {
                (picture, cardCopy) = await EncodeGalleryAsync(stream);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to process gallery image using SkiaSharp: {FileName}", fileName);
                throw;
            }

            var name = $"{GalleryFolder}/{Guid.NewGuid():N}";

            return await this.PutWithCopyAsync(picture, name + ".webp", cardCopy, name + GalleryCardSuffix, "gallery");
        }

        public async Task DeleteGalleryImageAsync(string imageUrl)
        {
            if (!IsGalleryUpload(imageUrl))
            {
                return;
            }

            await this.DeleteQuietlyAsync(imageUrl);
            await this.DeleteQuietlyAsync(AppendCopySuffix(imageUrl, GalleryCardSuffix));
        }

        public string GetGalleryCardCopyPublicUrl(string imageUrl)
        {
            return IsGalleryUpload(imageUrl) || IsBuiltInGallery(imageUrl)
                ? AppendCopySuffix(imageUrl, GalleryCardSuffix)
                : null;
        }

        private static bool IsUpload(string imageUrl)
        {
            return imageUrl != null && UploadedName.IsMatch(imageUrl);
        }

        private static string SmallCopyOf(string uploadUrl)
        {
            return AppendCopySuffix(uploadUrl, $"-{SmallCopyWidth}.webp");
        }

        private static bool IsAreaUpload(string imageUrl)
        {
            return imageUrl != null && UploadedAreaName.IsMatch(imageUrl);
        }

        private static string AreaPhoneCopyOf(string uploadUrl)
        {
            return AppendCopySuffix(uploadUrl, AreaPhoneSuffix);
        }

        private static bool IsGalleryUpload(string imageUrl)
        {
            return imageUrl != null && UploadedGalleryName.IsMatch(imageUrl);
        }

        private static bool IsBuiltInGallery(string imageUrl)
        {
            return imageUrl != null && BuiltInGalleryName.IsMatch(imageUrl);
        }

        private static string AppendCopySuffix(string uploadUrl, string suffix)
        {
            return uploadUrl.Substring(0, uploadUrl.Length - ".webp".Length) + suffix;
        }

        // The slug is in the name only so that the bucket reads sensibly. Kept to what a web address
        // carries as it is, whatever an admin typed into the service's name.
        private static string NameFrom(string slug, string fallback)
        {
            var name = Regex.Replace((slug ?? string.Empty).ToLowerInvariant(), "[^a-z0-9-]", string.Empty);
            return name.Length == 0 ? fallback : name;
        }

        // An area's picture, 1600 x 700, and its 928-wide phone copy, both as WebP. The picture is
        // made to cover 1600 x 700 and its middle is kept, as a page draws it (object-fit: cover,
        // centred); one smaller than that is drawn larger, which is the simplest answer for a
        // picture the admin can always replace. The phone copy is the middle of the result.
        private static async Task<(byte[] Picture, byte[] PhoneCopy)> EncodeAreaAsync(Stream stream)
        {
            using (var memoryStream = new MemoryStream())
            {
                await stream.CopyToAsync(memoryStream);
                memoryStream.Position = 0;

                using (var original = SKBitmap.Decode(memoryStream))
                {
                    if (original == null)
                    {
                        throw new ImageUploadValidationException("Failed to decode uploaded image.");
                    }

                    var scale = Math.Max((double)AreaWidth / original.Width, (double)AreaHeight / original.Height);
                    var sourceWidth = (float)(AreaWidth / scale);
                    var sourceHeight = (float)(AreaHeight / scale);
                    var source = SKRect.Create((original.Width - sourceWidth) / 2, (original.Height - sourceHeight) / 2, sourceWidth, sourceHeight);

                    using (var picture = new SKBitmap(AreaWidth, AreaHeight))
                    using (var phone = new SKBitmap())
                    {
                        using (var image = SKImage.FromBitmap(original))
                        using (var canvas = new SKCanvas(picture))
                        {
                            canvas.DrawImage(image, source, SKRect.Create(AreaWidth, AreaHeight), ResizeQuality);
                        }

                        var left = (AreaWidth - AreaPhoneWidth) / 2;
                        if (!picture.ExtractSubset(phone, SKRectI.Create(left, 0, AreaPhoneWidth, AreaHeight)))
                        {
                            throw new InvalidOperationException("Could not cut the phone copy of an area image.");
                        }

                        return (ToWebp(picture), ToWebp(phone));
                    }
                }
            }
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
                        throw new ImageUploadValidationException("Failed to decode uploaded image.");
                    }

                    SKBitmap targetBitmap = originalBitmap;
                    var isResized = false;

                    if (originalBitmap.Width > 1920)
                    {
                        var targetWidth = 1920;
                        var targetHeight = (int)Math.Round((double)originalBitmap.Height * targetWidth / originalBitmap.Width);
                        targetBitmap = originalBitmap.Resize(new SKImageInfo(targetWidth, targetHeight), ResizeQuality);
                        isResized = true;
                    }

                    try
                    {
                        var smallHeight = (int)Math.Round((double)targetBitmap.Height * SmallCopyWidth / targetBitmap.Width);
                        using (var small = targetBitmap.Resize(new SKImageInfo(SmallCopyWidth, smallHeight), ResizeQuality))
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

        // A gallery photo: a full copy at most 1600px on its longest side (keeping its aspect ratio
        // for the lightbox) and a 640 x 480 card copy cut from its middle. Re-encoding strips EXIF
        // metadata (such as GPS location), while respecting the camera's orientation tag first.
        private static async Task<(byte[] Picture, byte[] CardCopy)> EncodeGalleryAsync(Stream stream)
        {
            using (var memoryStream = new MemoryStream())
            {
                await stream.CopyToAsync(memoryStream);
                memoryStream.Position = 0;

                using (SKBitmap original = DecodeWithOrientation(memoryStream))
                {
                    SKBitmap fullBitmap = original;
                    bool fullResized = false;

                    int longestSide = Math.Max(original.Width, original.Height);
                    if (longestSide > GalleryMaxDimension)
                    {
                        double fullScale = (double)GalleryMaxDimension / longestSide;
                        int targetWidth = Math.Max(1, (int)Math.Round(original.Width * fullScale));
                        int targetHeight = Math.Max(1, (int)Math.Round(original.Height * fullScale));
                        fullBitmap = original.Resize(new SKImageInfo(targetWidth, targetHeight), ResizeQuality);
                        fullResized = true;
                    }

                    try
                    {
                        double cardScale = Math.Max((double)GalleryCardWidth / original.Width, (double)GalleryCardHeight / original.Height);
                        float sourceWidth = (float)(GalleryCardWidth / cardScale);
                        float sourceHeight = (float)(GalleryCardHeight / cardScale);
                        SKRect source = SKRect.Create((original.Width - sourceWidth) / 2f, (original.Height - sourceHeight) / 2f, sourceWidth, sourceHeight);

                        using (var cardBitmap = new SKBitmap(GalleryCardWidth, GalleryCardHeight))
                        {
                            using (var image = SKImage.FromBitmap(original))
                            using (var canvas = new SKCanvas(cardBitmap))
                            {
                                canvas.DrawImage(image, source, SKRect.Create(GalleryCardWidth, GalleryCardHeight), ResizeQuality);
                            }

                            return (ToWebp(fullBitmap, 82), ToWebp(cardBitmap, 80));
                        }
                    }
                    finally
                    {
                        if (fullResized)
                        {
                            fullBitmap.Dispose();
                        }
                    }
                }
            }
        }

        private static SKBitmap DecodeWithOrientation(MemoryStream memoryStream)
        {
            memoryStream.Position = 0;
            using (var codec = SKCodec.Create(memoryStream))
            {
                if (codec == null)
                {
                    throw new ImageUploadValidationException("Failed to decode uploaded image.");
                }

                SKBitmap bitmap = SKBitmap.Decode(codec);
                if (bitmap == null || bitmap.Width <= 0 || bitmap.Height <= 0)
                {
                    bitmap?.Dispose();
                    throw new ImageUploadValidationException("Failed to decode uploaded image.");
                }

                SKEncodedOrigin origin = codec.EncodedOrigin;
                if (origin == SKEncodedOrigin.TopLeft || origin == SKEncodedOrigin.Default)
                {
                    return bitmap;
                }

                bool swapDimensions = origin == SKEncodedOrigin.LeftTop
                    || origin == SKEncodedOrigin.RightTop
                    || origin == SKEncodedOrigin.RightBottom
                    || origin == SKEncodedOrigin.LeftBottom;

                int width = swapDimensions ? bitmap.Height : bitmap.Width;
                int height = swapDimensions ? bitmap.Width : bitmap.Height;
                var rotated = new SKBitmap(width, height, bitmap.ColorType, bitmap.AlphaType);

                using (var canvas = new SKCanvas(rotated))
                {
                    switch (origin)
                    {
                        case SKEncodedOrigin.TopRight:
                            canvas.Scale(-1, 1, width / 2f, height / 2f);
                            break;
                        case SKEncodedOrigin.BottomRight:
                            canvas.RotateDegrees(180, width / 2f, height / 2f);
                            break;
                        case SKEncodedOrigin.BottomLeft:
                            canvas.Scale(1, -1, width / 2f, height / 2f);
                            break;
                        case SKEncodedOrigin.LeftTop:
                            canvas.RotateDegrees(90);
                            canvas.Scale(1, -1);
                            break;
                        case SKEncodedOrigin.RightTop:
                            canvas.Translate(width, 0);
                            canvas.RotateDegrees(90);
                            break;
                        case SKEncodedOrigin.RightBottom:
                            canvas.Translate(width, height);
                            canvas.RotateDegrees(-90);
                            canvas.Scale(1, -1);
                            break;
                        case SKEncodedOrigin.LeftBottom:
                            canvas.Translate(0, height);
                            canvas.RotateDegrees(-90);
                            break;
                    }

                    using (var image = SKImage.FromBitmap(bitmap))
                    {
                        var bounds = SKRect.Create(bitmap.Width, bitmap.Height);
                        canvas.DrawImage(image, bounds, bounds, ResizeQuality);
                    }
                }

                bitmap.Dispose();
                return rotated;
            }
        }

        private static byte[] ToWebp(SKBitmap bitmap, int quality = 80)
        {
            using (var image = SKImage.FromBitmap(bitmap))
            using (SKData data = image.Encode(SKEncodedImageFormat.Webp, quality))
            {
                return data.ToArray();
            }
        }

        // The address of a built-in picture's copy of the given width, when the copy is in the
        // site's files; null for any other address.
        private string BuiltInCopy(string imageUrl, int width)
        {
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

            var copyName = $"{slug}-hero-{width}.webp";
            return File.Exists(Path.Combine(this.builtInDirectory, copyName)) ? BuiltInFolder + copyName : null;
        }

        private void CheckFileType(string fileName, string contentType)
        {
            var extension = Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant();
            if (!this.allowedExtensions.Contains(extension) || !this.allowedContentTypes.Contains((contentType ?? string.Empty).ToLowerInvariant()))
            {
                throw new ImageUploadValidationException("Unsupported file type. Only JPEG, PNG, and WEBP images are allowed.");
            }
        }

        // A picture and its copy go up together, and the picture's address is returned.
        private async Task<string> PutWithCopyAsync(byte[] picture, string pictureKey, byte[] copy, string copyKey, string slug)
        {
            string pictureUrl;
            using (var pictureStream = new MemoryStream(picture))
            {
                pictureUrl = await this.r2Service.PutFileAsync(pictureStream, pictureKey, "image/webp");
            }

            try
            {
                using (var copyStream = new MemoryStream(copy))
                {
                    await this.r2Service.PutFileAsync(copyStream, copyKey, "image/webp");
                }
            }
            catch (Exception ex)
            {
                // The upload has failed as far as the admin is told, so the picture that did go up
                // is taken down again rather than left with nothing pointing at it.
                this.logger.LogError(ex, "Failed to upload the copy of an image for slug: {Slug}", slug);
                await this.DeleteQuietlyAsync(pictureUrl);
                throw;
            }

            return pictureUrl;
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
                this.logger.LogWarning(ex, "Could not delete image {ImageUrl} from the bucket", url);
            }
        }
    }
}
