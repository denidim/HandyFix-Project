namespace HandyFix.Data.Models
{
    using System;
    using System.ComponentModel.DataAnnotations;

    using HandyFix.Data.Common.Models;

    /// <summary>
    /// A bookable hour of business capacity. Deliberately carries no technician: capacity is
    /// independent of who ends up executing the job, and technicians are fluid (largely
    /// self-employed contractors). The assignment lives solely on <see cref="Booking.TechnicianId"/>
    /// and is made by an admin after the customer has booked and paid.
    /// </summary>
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

        [Timestamp]
        public byte[] RowVersion { get; set; }

        public TimeSpan Duration => this.EndTime - this.StartTime;

        public Guid? ServiceId { get; set; }

        public virtual Service Service { get; set; }

        public Guid? BookingId { get; set; }

        public virtual Booking Booking { get; set; }
    }
}
