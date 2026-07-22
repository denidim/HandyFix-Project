namespace HandyFix.Web.ViewModels.Administration.Calendar
{
    using System;
    using System.Collections.Generic;

    using HandyFix.Data.Models;

    public class CalendarIndexViewModel
    {
        public DateTime TargetDate { get; set; }

        public IEnumerable<AvailabilitySlot> Slots { get; set; } = new List<AvailabilitySlot>();
    }
}
