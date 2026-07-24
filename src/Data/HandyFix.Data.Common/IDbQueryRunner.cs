namespace HandyFix.Data.Common
{
    using System;
    using System.Threading.Tasks;

    using Microsoft.EntityFrameworkCore.Storage;

    public interface IDbQueryRunner : IDisposable
    {
        Task RunQueryAsync(string query, params object[] parameters);

        Task<IDbContextTransaction> BeginTransactionAsync();
    }
}
