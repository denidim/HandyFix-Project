namespace HandyFix.Web.ViewModels.Administration.Calendar
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using HandyFix.Common;
    using HandyFix.Data.Models;
    using HandyFix.Web.ViewModels.Booking;

    public class CalendarIndexViewModel
    {
        // Built from Jobs the first time a slot asks, instead of searching the jobs once for
        // every slot of the day as the page did until PROJECT_STATE.md Section 3dj.
        private ILookup<DateTime, BookingDetailsViewModel> jobsByStart;
        private HashSet<Guid> waitingForDeposit;

        public DateTime TargetDate { get; set; }

        public IEnumerable<AvailabilitySlot> Slots { get; set; } = new List<AvailabilitySlot>();

        /// <summary>
        /// Every job that is on or done that day, written-in ones too. A written-in job holds no
        /// slot, so the slots alone would show its hour as free.
        /// </summary>
        public IEnumerable<BookingDetailsViewModel> Jobs { get; set; } = new List<BookingDetailsViewModel>();

        // The arrows either side of "Jobs on …" and of "Slots for …".
        public CalendarArrowViewModel JobsBefore { get; set; } = new CalendarArrowViewModel();

        public CalendarArrowViewModel JobsAfter { get; set; } = new CalendarArrowViewModel();

        public CalendarArrowViewModel SlotsBefore { get; set; } = new CalendarArrowViewModel();

        public CalendarArrowViewModel SlotsAfter { get; set; } = new CalendarArrowViewModel();

        /// <summary>
        /// The day's jobs that start at this hour, in the order of <see cref="Jobs"/>. An hour
        /// still on sale can have one: a written-in job holds no slot.
        /// </summary>
        public IEnumerable<BookingDetailsViewModel> JobsAt(DateTime start)
        {
            this.jobsByStart ??= this.Jobs.ToLookup(j => j.ScheduledTime);
            return this.jobsByStart[start];
        }

        /// <summary>
        /// Whether the booking that holds a slot is still waiting for its deposit.
        /// </summary>
        public bool IsWaitingForDeposit(Guid? bookingId)
        {
            this.waitingForDeposit ??= this.Jobs.Where(j => j.JobLabel == JobLabels.WaitingForDeposit).Select(j => j.Id).ToHashSet();
            return bookingId.HasValue && this.waitingForDeposit.Contains(bookingId.Value);
        }
    }
}
