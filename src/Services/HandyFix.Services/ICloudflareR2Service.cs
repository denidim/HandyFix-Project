namespace HandyFix.Services
{
    using System.IO;
    using System.Threading.Tasks;

    public interface ICloudflareR2Service
    {
        // A visitor's file, under a key of the service's choosing: "{folder}/{random}_{fileName}".
        Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType, string folder);

        // A file under the key it is given, for a caller that names its own files. Returns the
        // file's public address.
        Task<string> PutFileAsync(Stream fileStream, string key, string contentType);

        Task DeleteFileAsync(string fileUrl);
    }
}
