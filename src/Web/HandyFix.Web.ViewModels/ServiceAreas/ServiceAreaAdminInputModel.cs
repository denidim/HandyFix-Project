namespace HandyFix.Web.ViewModels.ServiceAreas
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;

    using HandyFix.Data.Models;
    using HandyFix.Services.Mapping;
    using HandyFix.Web.ViewModels.Administration.Services;
    using HandyFix.Web.ViewModels.Validation;

    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc.ModelBinding;

    public class ServiceAreaAdminInputModel : IMapFrom<ServiceArea>
    {
        public Guid? Id { get; set; }

        // An area not saved yet has no id: the form then creates one rather than updating one.
        // The Create and Edit pages used to tell their shared form in ViewData.
        public bool IsNew => this.Id == null;

        // The picture the admin uploaded, if any, as the database holds it. Never read from the
        // form: only HeroImageFile changes the picture.
        [BindNever]
        public string HeroImageUrl { get; set; }

        // Set by the server for the Edit page: the picture the area's page shows now (the upload,
        // else the one that came with the site, else none).
        [BindNever]
        public string CurrentPictureUrl { get; set; }

        [Display(Name = "Area picture")]
        [MaxFileSize(20 * 1024 * 1024)]
        public IFormFile HeroImageFile { get; set; }

        [Required(ErrorMessage = "Area name is required.")]
        [MinLength(2, ErrorMessage = "Name must be at least 2 characters.")]
        [MaxLength(100, ErrorMessage = "Name cannot exceed 100 characters.")]
        public string Name { get; set; }

        // The last part of the page address, /Areas/{slug}. Made from the name when the area is
        // created and never changed after, so the address stays the same when a typo in the name
        // is fixed (PROJECT_STATE.md Section 3dn). Never read from the form.
        [BindNever]
        public string Slug { get; set; }

        // Worked out when the area is created; the Edit page shows it. Never read from the form.
        [BindNever]
        public int DriveTimeMinutes { get; set; }

        [Display(Name = "Featured area")]
        public bool IsFeatured { get; set; }

        // The booking form takes a booking only for a postcode in a district some area lists, so
        // this is what decides where the site accepts bookings. Left empty, it is filled from the
        // town's name when the area is saved.
        [Display(Name = "Postcode districts")]
        [MaxLength(200, ErrorMessage = "Postcode districts cannot exceed 200 characters.")]
        [RegularExpression(
            UkPostcode.DistrictListPattern,
            ErrorMessage = "List the districts with commas between them, for example KT5, KT6, KT7. A district is the first half of a postcode.")]
        public string PostcodeDistricts { get; set; }

        // Which side of its dot the name goes on, on the coverage diagram. Empty is "Automatic":
        // chosen from the direction (PROJECT_STATE.md Section 3dm). Only the Edit page shows it.
        [Display(Name = "Label on the diagram")]
        public DiagramLabelSide? DiagramLabelSide { get; set; }

        // Set by the server: the area's direction on the diagram, set when it is saved from its
        // first postcode district. Empty when it is not on the diagram.
        [BindNever]
        public double? DiagramBearing { get; set; }

        // Set by the server for the Edit page: every area, to draw the diagram.
        [BindNever]
        public IEnumerable<ServiceAreaViewModel> DiagramAreas { get; set; }

        [Required(ErrorMessage = "Write a few lines about the town.")]
        [MinLength(20, ErrorMessage = "Write at least 20 characters about the town.")]
        [MaxLength(2000, ErrorMessage = "Keep this under 2000 characters.")]
        [Display(Name = "About the town")]
        public string IntroCopy { get; set; }

        [Required(ErrorMessage = "Name a few local streets or landmarks.")]
        [MinLength(20, ErrorMessage = "Write at least 20 characters about the local streets.")]
        [MaxLength(2000, ErrorMessage = "Keep this under 2000 characters.")]
        [Display(Name = "Local streets and landmarks")]
        public string LocalNeighbourhoodsCopy { get; set; }

        public List<ServiceAreaFaqInputModel> Faqs { get; set; } = new List<ServiceAreaFaqInputModel>();
    }
}
