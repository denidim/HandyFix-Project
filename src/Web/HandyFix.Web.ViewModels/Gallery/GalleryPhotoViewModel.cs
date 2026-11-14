namespace HandyFix.Web.ViewModels.Gallery
{
    using System;

    using HandyFix.Data.Models;
    using HandyFix.Services.Mapping;

    using Mapster;

    using IMapFromGalleryPhoto = HandyFix.Services.Mapping.IMapFrom<HandyFix.Data.Models.GalleryPhoto>;

    public class GalleryPhotoViewModel : IMapFromGalleryPhoto, IHaveCustomMappings
    {
        public Guid Id { get; set; }

        public string ImageUrl { get; set; }

        // 640 x 480 copy for grid cards and the homepage strip; filled in by GalleryService
        // and falls back to ImageUrl when none exists.
        public string CardImageUrl { get; set; }

        public string Caption { get; set; }

        public string StageBadge { get; set; }

        public bool ShowOnHomePage { get; set; }

        public int DisplayOrder { get; set; }

        public DateTime CreatedOn { get; set; }

        public void CreateMappings(TypeAdapterConfig config)
        {
            config.NewConfig<GalleryPhoto, GalleryPhotoViewModel>()
                .Ignore(dest => dest.CardImageUrl);
        }
    }
}
