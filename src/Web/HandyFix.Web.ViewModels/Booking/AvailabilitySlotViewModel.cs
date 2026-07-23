namespace HandyFix.Web.ViewModels.Booking
{
    using System;

    using HandyFix.Data.Models;
    using HandyFix.Services.Mapping;

    public class AvailabilitySlotViewModel : IMapFrom<AvailabilitySlot>
    {
        public Guid Id { get; set; }

        public DateTime StartTime { get; set; }

        public DateTime EndTime { get; set; }

        public bool IsBooked { get; set; }

        public bool IsBlocked { get; set; }

        public bool IsAvailable => !this.IsBooked && !this.IsBlocked;

        public string FormattedTime => $"{this.StartTime:HH:mm} - {this.EndTime:HH:mm}";
    }
}
