namespace HandyFix.Web.ViewModels.Reviews
{
    using System;

    using HandyFix.Data.Models;
    using HandyFix.Services.Mapping;

    public class ReviewViewModel : IMapFrom<Review>
    {
        public Guid Id { get; set; }

        public string CustomerName { get; set; }

        public string Comment { get; set; }

        public int Rating { get; set; }

        public bool IsApproved { get; set; }

        public DateTime CreatedOn { get; set; }
    }
}
