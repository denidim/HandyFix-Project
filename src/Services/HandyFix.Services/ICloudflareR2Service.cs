namespace HandyFix.Services
{
    using System.IO;
    using System.Threading.Tasks;

    public interface ICloudflareR2Service
    {
        Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType);
    }
}
