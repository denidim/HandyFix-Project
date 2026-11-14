namespace HandyFix.Web.ViewModels.Gallery
{
    using System;
    using System.Collections.Generic;

    public class GalleryListViewModel
    {
        public IReadOnlyList<GalleryPhotoViewModel> Photos { get; set; } = Array.Empty<GalleryPhotoViewModel>();
    }
}
