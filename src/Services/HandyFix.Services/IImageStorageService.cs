namespace HandyFix.Services
{
    using System.IO;
    using System.Threading.Tasks;

    public interface IImageStorageService
    {
        Task<bool> SaveServiceImageAsync(Stream stream, string fileName, string contentType, string slug);

        void DeleteServiceImage(string slug);

        void RenameServiceImage(string oldSlug, string newSlug);

        bool ServiceImageExists(string slug);
    }
}
