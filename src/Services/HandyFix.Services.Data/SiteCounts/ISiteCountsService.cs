namespace HandyFix.Services.Data.SiteCounts
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using HandyFix.Data.Models;

    // The website's count of what its visitors do, a sum for each day (PROJECT_STATE.md
    // Section 3fa).
    public interface ISiteCountsService
    {
        // One more of this kind on this day.
        Task AddAsync(SiteCountKind kind, DateOnly day);

        // Each kind's sum from that day on; of every day counted when no day is given. A kind
        // that never happened is not in it.
        Task<IReadOnlyDictionary<SiteCountKind, int>> GetTotalsAsync(DateOnly? since);

        // The first day anything was counted; nothing before the first count.
        Task<DateOnly?> GetFirstDayAsync();
    }
}
