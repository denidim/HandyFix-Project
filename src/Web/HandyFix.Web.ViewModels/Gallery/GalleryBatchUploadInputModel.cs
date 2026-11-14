namespace HandyFix.Web.ViewModels.Gallery
{
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;

    using Microsoft.AspNetCore.Http;

    public class GalleryBatchUploadInputModel
    {
        [Required(ErrorMessage = "Please select at least one photo to upload.")]
        public IList<IFormFile> Photos { get; set; } = new List<IFormFile>();

        [Required(ErrorMessage = "Please enter a short description of the job.")]
        [MaxLength(90, ErrorMessage = "Keep the description under 90 characters so all cards stay even.")]
        public string Caption { get; set; }

        [MaxLength(10)]
        public string StageBadge { get; set; }

        public bool ShowOnHomePage { get; set; }
    }
}
