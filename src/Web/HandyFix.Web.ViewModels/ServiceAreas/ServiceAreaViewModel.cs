namespace HandyFix.Web.ViewModels.ServiceAreas
{
    using System;

    using HandyFix.Data.Models;
    using HandyFix.Services.Mapping;

    public class ServiceAreaViewModel : IMapFrom<ServiceArea>
    {
        public Guid Id { get; set; }

        public string Slug { get; set; }

        public string Name { get; set; }

        public int DriveTimeMinutes { get; set; }

        public bool IsFeatured { get; set; }

        public string HeroImageUrl { get; set; }

        public double? DiagramBearing { get; set; }

        public DiagramLabelSide? DiagramLabelSide { get; set; }

        public string DiagramLabel { get; set; }

        // Set before a card is drawn: the picture it shows, or null for the general one
        // (PictureLookups.AreaPictureOf).
        public string PictureUrl { get; set; }
    }
}
