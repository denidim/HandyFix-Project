namespace HandyFix.Web.ViewModels.Administration.Calendar
{
    using System;
    using System.Collections.Generic;

    using HandyFix.Data.Models;
    using HandyFix.Web.ViewModels.Booking;

    public class CalendarIndexViewModel
    {
        public DateTime TargetDate { get; set; }

        public IEnumerable<AvailabilitySlot> Slots { get; set; } = new List<AvailabilitySlot>();

        /// <summary>
        /// Every job that is on or done that day, written-in ones too. A written-in job holds no
        /// slot, so the slots alone would show its hour as free.
        /// </summary>
        public IEnumerable<BookingDetailsViewModel> Jobs { get; set; } = new List<BookingDetailsViewModel>();

        public CalendarNeighbourDayViewModel PreviousDay { get; set; } = new CalendarNeighbourDayViewModel();

        public CalendarNeighbourDayViewModel NextDay { get; set; } = new CalendarNeighbourDayViewModel();
    }
}
