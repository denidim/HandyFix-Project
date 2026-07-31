namespace HandyFix.Web.ViewModels.Administration.Technicians
{
    using System;

    using HandyFix.Data.Models;

    using IMapFromTechnician = HandyFix.Services.Mapping.IMapFrom<HandyFix.Data.Models.Technician>;

    /// <summary>
    /// One entry in a technician picker. Deliberately minimal - it exists so views can bind a
    /// dropdown without handing the raw <see cref="Technician"/> entity to the view layer.
    /// </summary>
    public class TechnicianOptionViewModel : IMapFromTechnician
    {
        public Guid Id { get; set; }

        public string FirstName { get; set; }

        public string LastName { get; set; }

        public bool IsActive { get; set; }

        public string FullName => $"{this.FirstName} {this.LastName}";
    }
}
