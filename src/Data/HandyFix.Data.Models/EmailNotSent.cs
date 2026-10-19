namespace HandyFix.Data.Models
{
    using System;
    using System.ComponentModel.DataAnnotations;

    using HandyFix.Data.Common.Models;

    /// <summary>
    /// An email the site could not send: which one, when (<see cref="BaseModel{TKey}.CreatedOn"/>),
    /// and the job or the enquiry it was about. The admin dashboard lists the last week's and an
    /// enquiry's page shows its own; a job's page has a history line for each as well
    /// (PROJECT_STATE.md Section 3cq). Until then a failed email was a line in the server's log and
    /// nothing else. Written once and never changed, which is why this is not a deletable model;
    /// an enquiry deleted for good takes its rows with it.
    /// </summary>
    public class EmailNotSent : BaseModel<Guid>
    {
        // No id is set here, for the reason BookingHistoryEntry gives: a row is added to a job or
        // an enquiry that is already loaded, and the key is filled in as it is saved.
        [Required(ErrorMessage = "The {0} field is required.")]
        [MaxLength(200, ErrorMessage = "The {0} field cannot exceed 200 characters.")]
        public string What { get; set; } = null!;

        public Guid? BookingId { get; set; }

        public virtual Booking Booking { get; set; }

        public Guid? InquiryId { get; set; }

        public virtual Inquiry Inquiry { get; set; }
    }
}
