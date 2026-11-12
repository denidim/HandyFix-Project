namespace HandyFix.Web.ViewModels.Reviews
{
    using System;
    using System.Globalization;
    using System.Linq;

    using HandyFix.Data.Models;
    using HandyFix.Services.Mapping;

    public class ReviewViewModel : IMapFrom<Review>
    {
        public Guid Id { get; set; }

        public string CustomerName { get; set; }

        public string Comment { get; set; }

        public int Rating { get; set; }

        public bool IsApproved { get; set; }

        public string Town { get; set; }

        public string JobDone { get; set; }

        public DateTime? ReviewedOn { get; set; }

        public DateTime CreatedOn { get; set; }

        // "Sarah K." gives "SK"; a single name gives its first letter.
        public string Initials
        {
            get
            {
                var names = (this.CustomerName ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
                return string.Concat(names.Take(2).Select(name => char.ToUpperInvariant(name[0])));
            }
        }

        // "Kingston upon Thames · Boiler service", or whichever half is known, or null.
        public string TownAndJob
        {
            get
            {
                var parts = new[] { this.Town, this.JobDone }.Where(part => !string.IsNullOrWhiteSpace(part)).ToArray();
                return parts.Length == 0 ? null : string.Join(" · ", parts);
            }
        }

        // The month and year only: Google shows "2 weeks ago", not a day, so a day would be made up.
        public string GoogleMonth =>
            (this.ReviewedOn ?? this.CreatedOn).ToString("MMMM yyyy", CultureInfo.InvariantCulture);
    }
}
