namespace HandyFix.Services.Data.ServiceAreas
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public interface IServiceAreasService
    {
        Task<IEnumerable<T>> GetAllAsync<T>(bool featuredFirst = true);

        Task<T> GetBySlugAsync<T>(string slug);

        Task<IEnumerable<T>> GetNearestAsync<T>(Guid excludeAreaId, int take = 4);
    }
}
