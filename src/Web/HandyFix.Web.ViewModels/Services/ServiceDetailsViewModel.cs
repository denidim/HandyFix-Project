namespace HandyFix.Web.ViewModels.Services
{
    using System;

    using HandyFix.Data.Models;
    using HandyFix.Services.Mapping;

    public class ServiceDetailsViewModel : IMapFrom<Service>
    {
        public Guid Id { get; set; }

        public string Slug { get; set; }

        public string Name { get; set; }

        public string Description { get; set; }

        public decimal BasePrice { get; set; }

        public int EstimatedDurationMinutes { get; set; }

        public string CategoryName { get; set; }

        public string CategorySlug { get; set; }
    }
}
