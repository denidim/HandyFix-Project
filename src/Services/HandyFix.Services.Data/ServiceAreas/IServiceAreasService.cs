namespace HandyFix.Services.Data.ServiceAreas
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using HandyFix.Web.ViewModels.ServiceAreas;

    public interface IServiceAreasService
    {
        Task<IEnumerable<T>> GetAllAsync<T>(bool featuredFirst = true);

        Task<T> GetBySlugAsync<T>(string slug);

        Task<T> GetByIdAsync<T>(Guid id);

        Task<IEnumerable<T>> GetNearestAsync<T>(Guid excludeAreaId, int take = 4);

        Task<Guid> CreateAsync(ServiceAreaAdminInputModel model);

        Task UpdateAsync(Guid id, ServiceAreaAdminInputModel model);

        /// <summary>
        /// Permanently removes the area and its FAQs. Hard delete on purpose: the unique index on
        /// ServiceArea.Slug is not filtered on IsDeleted, so a soft-deleted row keeps occupying its
        /// slug and blocks both the seeder and the admin form from ever reusing it.
        /// </summary>
        Task DeleteAsync(Guid id);

        /// <summary>
        /// Checks the slug against soft-deleted rows too, because the unique index covers them.
        /// </summary>
        Task<bool> SlugExistsAsync(string slug, Guid? excludeAreaId = null);

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
