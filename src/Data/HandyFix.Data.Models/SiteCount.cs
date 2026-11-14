namespace HandyFix.Data.Models
{
    using System;

    using Microsoft.EntityFrameworkCore;

    /// <summary>
    /// How many times one thing happened on the website in one day: a visit, the booking page
    /// opened, a time picked, Call or WhatsApp tapped (PROJECT_STATE.md Section 3fa). The admin
    /// Dashboard adds the days up. A row is a sum and nothing else: it holds no address, no
    /// browser and nothing that tells one visitor from another, which is why it is not built on
    /// the base model with its dates, and why its key is the day and the kind, of which there
    /// can be only one row.
    /// </summary>
    [PrimaryKey(nameof(Day), nameof(Kind))]
    public class SiteCount
    {
        // The business's day, not the server's: the server keeps UTC.
        public DateOnly Day { get; set; }

        public SiteCountKind Kind { get; set; }

        public int Count { get; set; }
    }
}
