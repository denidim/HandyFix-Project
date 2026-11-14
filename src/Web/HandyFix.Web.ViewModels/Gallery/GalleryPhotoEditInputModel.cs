namespace HandyFix.Web.ViewModels.Gallery
{
    using System;
    using System.ComponentModel.DataAnnotations;

    using HandyFix.Data.Models;
    using HandyFix.Services.Mapping;

    public class GalleryPhotoEditInputModel : IMapFrom<GalleryPhoto>
    {
        public Guid Id { get; set; }

        public string ImageUrl { get; set; }

        [Required(ErrorMessage = "Please enter a short description of the job.")]
        [MaxLength(90, ErrorMessage = "Keep the description under 90 characters so all cards stay even.")]
        public string Caption { get; set; }

        [MaxLength(10)]
        public string StageBadge { get; set; }

        public bool ShowOnHomePage { get; set; }

        public int DisplayOrder { get; set; }
    }
}
