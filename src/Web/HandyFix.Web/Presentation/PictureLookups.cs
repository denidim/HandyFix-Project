namespace HandyFix.Web.Presentation
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;

    using HandyFix.Services;
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
        /// The strip of an area's picture a phone is sent, or null when there is none. A phone
        /// draws the picture to fill a box 400px tall, so only a strip from its middle shows; the
        /// strip is cut out by tools/image-gen/make-phone-copies.mjs (PROJECT_STATE Section 3cz).
        /// An area added in the admin panel has none and is sent the picture itself.
        /// </summary>
        public static string PhonePictureOf(IWebHostEnvironment environment, string areaSlug)
        {
            var picture = $"/images/areas/{areaSlug}-hero-phone.webp";
            return Regex.IsMatch(areaSlug ?? string.Empty, "^[a-z0-9-]+$") && environment.WebRootFileProvider.GetFileInfo(picture).Exists
                ? picture
                : null;
        }

        /// <summary>
        /// Whether an area's picture is in place: step 2 of adding an area, which its Edit page
        /// says has been done or not.
        /// </summary>
        public static bool HasHeroImage(IWebHostEnvironment environment, string areaSlug)
        {
            return !string.IsNullOrWhiteSpace(areaSlug)
                && File.Exists(Path.Combine(environment.WebRootPath, "images", "areas", $"{areaSlug}-hero.webp"));
        }
    }
}
