namespace HandyFix.Services
{
    using System.IO;
    using System.Threading.Tasks;

    public interface IImageStorageService
    {
        Task<string> SaveServiceImageAsync(Stream stream, string fileName, string contentType, string slug);

        void DeleteServiceImage(string slug);

        void RenameServiceImage(string oldSlug, string newSlug);

        bool ServiceImageExists(string slug);

        string GetServiceImagePublicUrl(string slug);

        /// <summary>
        /// The address of a service picture's small copy, for a page that shows the picture small;
        /// null when the picture has none, and the page sends the picture itself.
        /// </summary>
        string GetSmallCopyPublicUrl(string imageUrl);
    }
}
