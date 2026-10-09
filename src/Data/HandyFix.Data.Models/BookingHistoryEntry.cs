namespace HandyFix.Data.Models
{
    using System;
    using System.ComponentModel.DataAnnotations;

    using HandyFix.Data.Common.Models;

    /// <summary>
    /// One line of what happened to a job: booked, paid, technician picked, moved, cancelled,
    /// done. Written when it happens and never changed or deleted, which is why this is not a
    /// deletable model. <see cref="BaseModel{TKey}.CreatedOn"/> is when it happened.
    /// </summary>
    public class BookingHistoryEntry : BaseModel<Guid>
    {
        // No id is set here, unlike the other models, and that is on purpose. A line is added to
        // the History of a job that is already loaded, and Entity Framework takes a new object
        // it finds that way for an existing row if it arrives with a key: it then tries to
        // update a row that is not there. Left empty, the key is filled in as the line is saved.
        [Required(ErrorMessage = "The {0} field is required.")]
        public Guid BookingId { get; set; }

        public virtual Booking Booking { get; set; } = null!;

        [Required(ErrorMessage = "The {0} field is required.")]
        [MaxLength(700, ErrorMessage = "The {0} field cannot exceed 700 characters.")]
        public string Text { get; set; } = null!;
    }
}
