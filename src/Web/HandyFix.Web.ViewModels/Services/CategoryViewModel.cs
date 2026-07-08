namespace HandyFix.Web.ViewModels.Services
{
    using System;
    using System.Collections.Generic;

    using HandyFix.Data.Models;
    using HandyFix.Services.Mapping;

    public class CategoryViewModel : IMapFrom<ServiceCategory>
    {
        public Guid Id { get; set; }

        public string Name { get; set; }

        public string Description { get; set; }

        public string Slug => this.Name.Replace(" ", "-").ToLower();

        public virtual ICollection<ServiceViewModel> Services { get; set; }
    }
}
