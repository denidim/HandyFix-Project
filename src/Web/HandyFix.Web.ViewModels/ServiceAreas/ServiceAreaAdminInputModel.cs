namespace HandyFix.Web.ViewModels.ServiceAreas
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;

    using HandyFix.Data.Models;
    using HandyFix.Services.Mapping;

    public class ServiceAreaAdminInputModel : IMapFrom<ServiceArea>
    {
        public Guid? Id { get; set; }

        [Required(ErrorMessage = "Area name is required.")]
        [MinLength(2, ErrorMessage = "Name must be at least 2 characters.")]
        [MaxLength(100, ErrorMessage = "Name cannot exceed 100 characters.")]
        public string Name { get; set; }

        // Deliberately an explicit, editable field rather than being regenerated from Name on
        // every save (which is what ServicesService does). The area slug is load-bearing in three
        // separate places - the public URL /Areas/{slug}, the hero image filename
        // wwwroot/images/areas/{slug}-hero.webp, and the geometry key in _AreaCoverageMap.cshtml -
        // so silently rewriting it because someone fixed a typo in the name would break all three
        // at once. See docs/WORKFLOW_SERVICE_AREAS.md.
        [Required(ErrorMessage = "Slug is required.")]
        [MinLength(3, ErrorMessage = "Slug must be at least 3 characters.")]
        [MaxLength(100, ErrorMessage = "Slug cannot exceed 100 characters.")]
        [RegularExpression(
            "^[a-z0-9]+(?:-[a-z0-9]+)*$",
            ErrorMessage = "Slug may only contain lowercase letters, numbers and single hyphens (e.g. kingston-upon-thames).")]
        public string Slug { get; set; }

        [Required(ErrorMessage = "Region is required.")]
        [MaxLength(100, ErrorMessage = "Region cannot exceed 100 characters.")]
        public string Region { get; set; }

        [Display(Name = "Drive time (minutes)")]
        [Range(0, 180, ErrorMessage = "Drive time must be between 0 and 180 minutes.")]
        public int DriveTimeMinutes { get; set; }

        [Display(Name = "Display order")]
        [Range(0, 999, ErrorMessage = "Display order must be between 0 and 999.")]
        public int DisplayOrder { get; set; }

        [Display(Name = "Featured area")]
        public bool IsFeatured { get; set; }

        [Required(ErrorMessage = "Intro copy is required.")]
        [MinLength(20, ErrorMessage = "Intro copy must be at least 20 characters.")]
        [MaxLength(2000, ErrorMessage = "Intro copy cannot exceed 2000 characters.")]
        [Display(Name = "Intro copy")]
        public string IntroCopy { get; set; }

        [Required(ErrorMessage = "Local neighbourhoods copy is required.")]
        [MinLength(20, ErrorMessage = "Local neighbourhoods copy must be at least 20 characters.")]
        [MaxLength(2000, ErrorMessage = "Local neighbourhoods copy cannot exceed 2000 characters.")]
        [Display(Name = "Local neighbourhoods copy")]
        public string LocalNeighbourhoodsCopy { get; set; }

        public List<ServiceAreaFaqInputModel> Faqs { get; set; } = new List<ServiceAreaFaqInputModel>();
    }
}
