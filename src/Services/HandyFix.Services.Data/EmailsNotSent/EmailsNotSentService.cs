namespace HandyFix.Services.Data.EmailsNotSent
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Data.Common.Repositories;
    using HandyFix.Data.Models;
    using HandyFix.Services.Mapping;

    using Microsoft.EntityFrameworkCore;

    // Reads the record of the emails the site could not send, for the admin dashboard
    // (PROJECT_STATE.md Section 3cq). The rows are written by the services that send each email,
    // on the job or the enquiry it was about, in the same save as its history line.
    public class EmailsNotSentService : IEmailsNotSentService
    {
        private readonly IRepository<EmailNotSent> emailsNotSentRepository;

        public EmailsNotSentService(IRepository<EmailNotSent> emailsNotSentRepository)
        {
            this.emailsNotSentRepository = emailsNotSentRepository;
        }

        public async Task<IEnumerable<T>> GetSinceAsync<T>(DateTime sinceUtc)
        {
            return await this.emailsNotSentRepository.AllAsNoTracking()
                .Where(x => x.CreatedOn >= sinceUtc)
                .OrderByDescending(x => x.CreatedOn)
                .To<T>()
                .ToListAsync();
        }
    }
}
