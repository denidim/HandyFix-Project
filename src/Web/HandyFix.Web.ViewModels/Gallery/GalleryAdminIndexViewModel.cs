namespace HandyFix.Web.ViewModels.Gallery
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public class GalleryAdminIndexViewModel
    {
        public IReadOnlyList<GalleryPhotoViewModel> Photos { get; set; } = Array.Empty<GalleryPhotoViewModel>();

        public GalleryBatchUploadInputModel Upload { get; set; } = new GalleryBatchUploadInputModel();

        public int TotalCount => this.Photos.Count;

        public int HomePageCount => this.Photos.Count(p => p.ShowOnHomePage);
    }
}
