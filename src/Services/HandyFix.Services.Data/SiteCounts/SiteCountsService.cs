namespace HandyFix.Services.Data.SiteCounts
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Data.Common.Repositories;
    using HandyFix.Data.Models;

    using Microsoft.EntityFrameworkCore;

    public class SiteCountsService : ISiteCountsService
    {
        private readonly IRepository<SiteCount> siteCountRepository;

        public SiteCountsService(IRepository<SiteCount> siteCountRepository)
        {
            this.siteCountRepository = siteCountRepository;
        }

        public async Task AddAsync(SiteCountKind kind, DateOnly day)
        {
            if (await this.AddOneAsync(kind, day))
            {
                return;
            }

            // The day's first of its kind: the row is made, already counting one.
            var first = new SiteCount { Day = day, Kind = kind, Count = 1 };
            try
            {
                await this.siteCountRepository.AddAsync(first);
                await this.siteCountRepository.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Another visitor's count made the row in the same moment, and the key (the day
                // and the kind) refused a second one. The row is there now, so this one is added
                // to it. The refused row is let go first: deleting a row that was never saved
                // only stops the context from trying to save it again.
                this.siteCountRepository.Delete(first);
                await this.AddOneAsync(kind, day);
            }
        }

        public async Task<IReadOnlyDictionary<SiteCountKind, int>> GetTotalsAsync(DateOnly? since)
        {
            IQueryable<SiteCount> counts = this.siteCountRepository.AllAsNoTracking();
            if (since.HasValue)
            {
                counts = counts.Where(x => x.Day >= since.Value);
            }

            return await counts
                .GroupBy(x => x.Kind)
                .Select(x => new { Kind = x.Key, Count = x.Sum(c => c.Count) })
                .ToDictionaryAsync(x => x.Kind, x => x.Count);
        }

        public async Task<DateOnly?> GetFirstDayAsync()
        {
            return await this.siteCountRepository.AllAsNoTracking()
                .OrderBy(x => x.Day)
                .Select(x => (DateOnly?)x.Day)
                .FirstOrDefaultAsync();
        }

        // The adding is one statement the database runs by itself ("Count = Count + 1"), not a
        // read followed by a write: two visitors counted in the same moment would both read 5
        // and both write 6. False when the day has no row of this kind yet.
        private async Task<bool> AddOneAsync(SiteCountKind kind, DateOnly day)
        {
            int changed = await this.siteCountRepository.All()
                .Where(x => x.Day == day && x.Kind == kind)
                .ExecuteUpdateAsync(row => row.SetProperty(x => x.Count, x => x.Count + 1));

            return changed > 0;
        }
    }
}
