namespace HandyFix.Data.Models
{
    using System;

    using HandyFix.Data.Common.Models;

    public class AvailabilitySlot : BaseDeletableModel<Guid>
    {
        public AvailabilitySlot()
        {
            this.Id = Guid.NewGuid();
        }

        public DateTime StartTime { get; set; }

        public DateTime EndTime { get; set; }

        public bool IsBooked { get; set; }

        public bool IsBlocked { get; set; }

        public TimeSpan Duration => this.EndTime - this.StartTime;

        public Guid? TechnicianId { get; set; }

        public virtual Technician Technician { get; set; }

        public Guid? ServiceId { get; set; }

        public virtual Service Service { get; set; }

        public Guid? BookingId { get; set; }

        public virtual Booking Booking { get; set; }
    }
}
