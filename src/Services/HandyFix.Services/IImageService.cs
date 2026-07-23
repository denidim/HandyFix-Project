namespace HandyFix.Services
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using Microsoft.AspNetCore.Http;

    public interface IImageService
    {
        Task<IReadOnlyList<string>> UploadImagesAsync(IEnumerable<IFormFile> images, string folder);
    }
}
