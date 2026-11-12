namespace HandyFix.Services.Data.ServiceAreas
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using HandyFix.Web.ViewModels.ServiceAreas;

    using Microsoft.AspNetCore.Http;

    public interface IServiceAreasService
    {
        Task<IEnumerable<T>> GetAllAsync<T>(bool featuredFirst = true);

        Task<T> GetBySlugAsync<T>(string slug);

        Task<T> GetByIdAsync<T>(Guid id);

        Task<IEnumerable<T>> GetNearestAsync<T>(Guid excludeAreaId, int take = 4);

        /// <summary>
        /// Saves a new area: its page address from its name, its postcode districts from its name
        /// when none are typed, and its place on the diagram and drive time from its first
        /// district. Nothing is saved when the name is taken or the town or its district cannot
        /// be found; the result then says why, and under which field.
        /// </summary>
        Task<ServiceAreaSaveResult> CreateAsync(ServiceAreaAdminInputModel model);

        /// <summary>
        /// Saves the changes to an area. Its page address and drive time never change. Empty
        /// postcode districts are filled from the name; when no town has it, nothing is saved.
        /// </summary>
        Task<ServiceAreaSaveResult> UpdateAsync(Guid id, ServiceAreaAdminInputModel model);

        /// <summary>
        /// Whether the area has a place on the coverage diagram. Saving an area places it from its
        /// first postcode district; false when it has none, or the place could not be found.
        /// </summary>
        Task<bool> IsOnDiagramAsync(Guid id);

        /// <summary>
        /// The picture the admin uploads for an area, new or existing. The new picture goes up,
        /// the area points at it, and only then does the previous upload leave the bucket, so a
        /// failure at any step leaves the area with a picture. No file does nothing.
        /// </summary>
        Task SetHeroImageAsync(Guid areaId, IFormFile imageFile);

        /// <summary>
        /// Every postcode district online booking takes, each once, in order: the ones the areas
        /// list and every district within 15 miles of the base (PROJECT_STATE.md Section 3ek).
        /// </summary>
        Task<IReadOnlyList<string>> GetServedPostcodeDistrictsAsync();

        /// <summary>
        /// Whether a booking can be taken for this postcode: its district is one some area lists,
        /// or one within 15 miles of the base.
        /// </summary>
        Task<bool> IsPostcodeServedAsync(string postcode);

        /// <summary>
        /// Permanently removes the area and its FAQs, and its uploaded picture from the bucket
        /// afterwards. Hard delete on purpose: the unique index on
        /// ServiceArea.Slug is not filtered on IsDeleted, so a soft-deleted row keeps occupying its
        /// slug and blocks both the seeder and the admin form from ever reusing it.
        /// </summary>
        Task DeleteAsync(Guid id);

        /// <summary>
        /// Drops FAQ rows left entirely blank (mutating model.Faqs), then validates whatever
        /// survives against the same limits as the ServiceAreaFaq entity, so a row that passes
        /// here cannot fail at SaveChanges. Returns every violation found; an empty result means
        /// the surviving rows are all valid. Pruning happens first so returned error keys always
        /// match the index the row will actually render at.
        /// </summary>
        IEnumerable<ServiceAreaFaqValidationError> PruneAndValidateFaqs(ServiceAreaAdminInputModel model);
    }
}
