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

        /// <summary>
        /// The address of a built-in service picture's middle copy, for a card that shows it about
        /// a third of the picture's width; null when it has none (an upload never has), and the
        /// page sends the picture itself.
        /// </summary>
        string GetMediumCopyPublicUrl(string imageUrl);

        /// <summary>
        /// Makes an uploaded picture an area's: its middle cut to 1600 x 700, with a 928-wide
        /// phone copy from the middle of that. Both go to the photo bucket under a new name, and
        /// the picture's address is returned.
        /// </summary>
        Task<string> SaveAreaImageAsync(Stream stream, string fileName, string contentType, string slug);

        /// <summary>
        /// Takes an uploaded area picture and its phone copy out of the bucket. Any other address
        /// is left alone, and a delete that fails is logged, not thrown.
        /// </summary>
        Task DeleteAreaImageAsync(string imageUrl);

        /// <summary>
        /// The address of an uploaded area picture's phone copy; null for any other address.
        /// </summary>
        string GetAreaPhoneCopyPublicUrl(string imageUrl);
    }
}
