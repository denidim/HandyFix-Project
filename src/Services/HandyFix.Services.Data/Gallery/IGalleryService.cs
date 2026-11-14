namespace HandyFix.Services.Data.Gallery
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using HandyFix.Web.ViewModels.Gallery;

    public interface IGalleryService
    {
        Task<IReadOnlyList<GalleryPhotoViewModel>> GetAllAsync();

        Task<IReadOnlyList<GalleryPhotoViewModel>> GetHomePhotosAsync();

        Task<T> GetByIdAsync<T>(Guid id);

        Task<int> CreateBatchAsync(GalleryBatchUploadInputModel input);

        Task<bool> UpdateAsync(Guid id, GalleryPhotoEditInputModel input);

        Task<bool?> ToggleHomePageAsync(Guid id);

        Task<bool> MoveAsync(Guid id, int direction);

        Task<bool> DeleteAsync(Guid id);
    }
}
