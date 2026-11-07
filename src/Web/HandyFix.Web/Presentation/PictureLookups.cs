namespace HandyFix.Web.Presentation
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;

    using HandyFix.Services;
    using HandyFix.Web.ViewModels.ServiceAreas;
    using HandyFix.Web.ViewModels.Services;

    using Microsoft.AspNetCore.Hosting;

    /// <summary>
    /// The smaller copies of the pictures a page shows, and the area pictures that may not be
    /// there yet, looked up by the controller before the page is drawn. The views used to look
    /// them up while they were drawn, the home page each card picture twice (PROJECT_STATE.md
    /// Section 3dj).
    /// </summary>
    public static class PictureLookups
    {
        // For a page that shows the services' pictures small: the category page's list.
        public static List<ServiceViewModel> WithSmallCopies(this IEnumerable<ServiceViewModel> services, IImageStorageService storage)
        {
            var list = services.ToList();
            foreach (ServiceViewModel service in list)
            {
                service.SmallCopyUrl = storage.GetSmallCopyPublicUrl(service.ImageUrl);
            }

            return list;
        }

        // For a page that shows them on cards: the pricing cards and the home page's.
        public static List<ServiceViewModel> WithMediumCopies(this IEnumerable<ServiceViewModel> services, IImageStorageService storage)
        {
            var list = services.ToList();
            foreach (ServiceViewModel service in list)
            {
                service.MediumCopyUrl = storage.GetMediumCopyPublicUrl(service.ImageUrl);
            }

            return list;
        }

        /// <summary>
        /// The picture an area shows: the one the admin uploaded, else the one that came with the
        /// site for its slug; null when it has neither, and the page shows the general picture
        /// (PROJECT_STATE.md Section 3dl). Every page that shows an area's picture asks here.
        /// </summary>
        public static string AreaPictureOf(IWebHostEnvironment environment, string areaSlug, string uploadedUrl)
        {
            return !string.IsNullOrWhiteSpace(uploadedUrl)
                ? uploadedUrl
                : BuiltInAreaFile(environment, areaSlug, "-hero.webp");
        }

        /// <summary>
        /// The strip of that picture a phone is sent, or null when there is none. A phone draws
        /// the picture to fill a box 400px tall, so only a strip from its middle shows: an upload
        /// has its strip beside it in the bucket, and the pictures that came with the site have
        /// theirs from tools/image-gen/make-phone-copies.mjs (PROJECT_STATE Section 3cz).
        /// </summary>
        public static string AreaPhonePictureOf(IWebHostEnvironment environment, IImageStorageService storage, string areaSlug, string uploadedUrl)
        {
            return !string.IsNullOrWhiteSpace(uploadedUrl)
                ? storage.GetAreaPhoneCopyPublicUrl(uploadedUrl)
                : BuiltInAreaFile(environment, areaSlug, "-hero-phone.webp");
        }

        // For the area cards: each card's picture.
        public static List<ServiceAreaViewModel> WithPictures(this IEnumerable<ServiceAreaViewModel> areas, IWebHostEnvironment environment)
        {
            var list = areas.ToList();
            foreach (ServiceAreaViewModel area in list)
            {
                area.PictureUrl = AreaPictureOf(environment, area.Slug, area.HeroImageUrl);
            }

            return list;
        }

        private static string BuiltInAreaFile(IWebHostEnvironment environment, string areaSlug, string suffix)
        {
            var file = $"/images/areas/{areaSlug}{suffix}";
            return Regex.IsMatch(areaSlug ?? string.Empty, "^[a-z0-9-]+$") && environment.WebRootFileProvider.GetFileInfo(file).Exists
                ? file
                : null;
        }
    }
}
