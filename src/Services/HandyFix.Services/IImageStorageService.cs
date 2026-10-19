namespace HandyFix.Services
{
    using System.IO;
    using System.Threading.Tasks;

    public interface IImageStorageService
    {
        /// <summary>
        /// Puts an uploaded picture and its small copy in the photo bucket under a new name, and
        /// returns the picture's address. Nothing is written to the site's own files.
        /// </summary>
        Task<string> SaveServiceImageAsync(Stream stream, string fileName, string contentType, string slug);

        /// <summary>
        /// Takes an uploaded picture and its small copy out of the bucket. A built-in picture is
        /// left alone, and a delete that fails is logged, not thrown.
        /// </summary>
        Task DeleteServiceImageAsync(string imageUrl);

        /// <summary>
        /// The address of a service picture's small copy, for a page that shows the picture small;
        /// null when the picture has none, and the page sends the picture itself.
        /// </summary>
        string GetSmallCopyPublicUrl(string imageUrl);
    }
}
