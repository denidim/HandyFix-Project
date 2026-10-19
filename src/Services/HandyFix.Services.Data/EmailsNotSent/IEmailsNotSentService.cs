namespace HandyFix.Services.Data.EmailsNotSent
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public interface IEmailsNotSentService
    {
        // The emails the site could not send since a moment (UTC), the newest first.
        Task<IEnumerable<T>> GetSinceAsync<T>(DateTime sinceUtc);
    }
}
