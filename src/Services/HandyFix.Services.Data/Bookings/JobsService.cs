namespace HandyFix.Services.Data.Bookings
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Common;
    using HandyFix.Data.Common;
    using HandyFix.Data.Common.Repositories;
    using HandyFix.Data.Models;
    using HandyFix.Services.Data.Availability;
    using HandyFix.Services.Data.Common;
    using HandyFix.Services.Data.Emails;
    using HandyFix.Services.Data.Payments;
    using HandyFix.Services.Mapping;
    using HandyFix.Web.ViewModels.Booking;

    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.ChangeTracking;
    using Microsoft.EntityFrameworkCore.Storage;

    // What the admin does to a job once it is in the book: write one in, finish it, put a
    // technician on it, cancel or move it, put its details right, keep notes on it. The
    // website's side of a booking and the lists and counts stay in BookingsService. Whether an
    // action is allowed on a job is asked of BookingRules here, as the pages ask it.
    public class JobsService : IJobsService
    {
        private const int MaxCancelReasonLength = 500;

        // How a "Details changed" history line starts, and the sentence in it that keeps what
        // the job was described as before.
        private const string DetailsChangedPrefix = "Details changed.";
        private const string DescriptionSentence = "The job was described as: \"{0}\"";

        private readonly IDeletableEntityRepository<Booking> bookingRepository;
        private readonly IDeletableEntityRepository<Service> serviceRepository;
        private readonly IDeletableEntityRepository<AvailabilitySlot> slotRepository;
        private readonly IDeletableEntityRepository<BookingStatus> statusRepository;
        private readonly IDeletableEntityRepository<Technician> technicianRepository;
        private readonly IDeletableEntityRepository<BookingService> bookingServiceRepository;
        private readonly IAvailabilityService availabilityService;
        private readonly IPaymentsService paymentsService;
        private readonly IDbQueryRunner dbQueryRunner;
        private readonly IBookingEmails bookingEmails;

        public JobsService(
            IDeletableEntityRepository<Booking> bookingRepository,
            IDeletableEntityRepository<Service> serviceRepository,
            IDeletableEntityRepository<AvailabilitySlot> slotRepository,
            IDeletableEntityRepository<BookingStatus> statusRepository,
            IDeletableEntityRepository<Technician> technicianRepository,
            IDeletableEntityRepository<BookingService> bookingServiceRepository,
            IAvailabilityService availabilityService,
            IPaymentsService paymentsService,
            IDbQueryRunner dbQueryRunner,
            IBookingEmails bookingEmails)
        {
            this.bookingRepository = bookingRepository;
            this.serviceRepository = serviceRepository;
            this.slotRepository = slotRepository;
            this.statusRepository = statusRepository;
            this.technicianRepository = technicianRepository;
            this.bookingServiceRepository = bookingServiceRepository;
            this.availabilityService = availabilityService;
            this.paymentsService = paymentsService;
            this.dbQueryRunner = dbQueryRunner;
            this.bookingEmails = bookingEmails;
        }

        public async Task<Guid> CreateWrittenInJobAsync(JobInputModel model)
        {
            if (model.Source == null || model.Source == BookingSource.Website)
            {
                throw new InvalidOperationException("A job written in by the admin comes from somewhere other than the website.");
            }

            if (model.Date == null || model.Time == null)
            {
                throw new InvalidOperationException("A job needs a day and a time.");
            }

            // Booked from the start, with no deposit to wait for. "Pending" is a website booking
            // waiting for its deposit, and the sweep that abandons those after fifteen minutes
            // must never pick up a job the admin wrote in.
            BookingStatus bookedStatus = await this.statusRepository.All().FirstOrDefaultAsync(x => x.Name == BookingStatusNames.Approved);
            if (bookedStatus == null)
            {
                throw new InvalidOperationException($"Booking status '{BookingStatusNames.Approved}' is not seeded.");
            }

            Service service = model.ServiceId.HasValue
                ? await this.serviceRepository.All().FirstOrDefaultAsync(x => x.Id == model.ServiceId.Value)
                : null;

            DateTime start = model.Date.Value.Date + model.Time.Value;

            var booking = new Booking
            {
                CustomerFirstName = model.CustomerFirstName.Trim(),
                CustomerLastName = NullIfBlank(model.CustomerLastName),
                Email = NullIfBlank(model.Email),
                PhoneNumber = model.PhoneNumber.Trim(),
                Address = NullIfBlank(model.Address),
                ProblemDescription = NullIfBlank(model.ProblemDescription),
                StatusId = bookedStatus.Id,
                Source = model.Source.Value,
                HeardAbout = HeardAboutText.Checked(model.HeardAbout),

                // An hour in the calendar is a start time, and one hour is the least a customer
                // pays for. No slot is claimed: the admin blocks the hour in the calendar by hand,
                // as agreed for every job that is written in.
                ScheduledStart = start,
                ScheduledEnd = start.AddHours(1),
                TotalAmount = service?.BasePrice,
            };

            if (service != null)
            {
                booking.BookingServices.Add(new BookingService
                {
                    ServiceId = service.Id,
                    PriceAtBooking = service.BasePrice,
                    Quantity = 1,
                });
            }

            booking.History.Add(JobHistory.Line($"Written in by the admin. Came from: {model.Source.Value}."));

            await this.bookingRepository.AddAsync(booking);
            await this.bookingRepository.SaveChangesAsync();

            return booking.Id;
        }

        public async Task<bool> CompleteBookingAsync(Guid bookingId, decimal finalPrice)
        {
            Booking booking = await this.bookingRepository.All()
                .Include(x => x.Status)
                .Include(x => x.Payments).ThenInclude(p => p.Status)
                .FirstOrDefaultAsync(x => x.Id == bookingId);
            BookingStatus completedStatus = await this.statusRepository.All().FirstOrDefaultAsync(x => x.Name == BookingStatusNames.Completed);

            if (booking == null
                || completedStatus == null
                || !IsAPrice(finalPrice)
                || !BookingRules.CanComplete(booking.Status?.Name, booking.Source == BookingSource.Website, IsDepositPaid(booking)))
            {
                return false;
            }

            // The final price is what the money list is added up against: until it is typed in
            // the page can only show an estimate, and no job can be "Paid in full".
            booking.StatusId = completedStatus.Id;
            booking.FinalPrice = finalPrice;
            booking.History.Add(JobHistory.Line($"Marked done. Final price {JobHistory.Pounds(finalPrice)}."));

            await this.bookingRepository.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ChangeFinalPriceAsync(Guid bookingId, decimal finalPrice)
        {
            Booking booking = await this.bookingRepository.All()
                .Include(x => x.Status)
                .FirstOrDefaultAsync(x => x.Id == bookingId);

            if (booking == null
                || !IsAPrice(finalPrice)
                || !BookingRules.CanChangeFinalPrice(booking.Status?.Name))
            {
                return false;
            }

            if (booking.FinalPrice != finalPrice)
            {
                booking.History.Add(JobHistory.Line(booking.FinalPrice.HasValue
                    ? $"Final price changed from {JobHistory.Pounds(booking.FinalPrice.Value)} to {JobHistory.Pounds(finalPrice)}."
                    : $"Final price set to {JobHistory.Pounds(finalPrice)}."));
                booking.FinalPrice = finalPrice;
                await this.bookingRepository.SaveChangesAsync();
            }

            return true;
        }

        public async Task<TechnicianAssignmentResult> AssignTechnicianAsync(Guid bookingId, Guid? technicianId)
        {
            Booking booking = await this.bookingRepository.All()
                .Include(x => x.Status)
                .Include(x => x.Payments).ThenInclude(p => p.Status)
                .Include(x => x.Technician)
                .Include(x => x.AvailabilitySlot)
                .FirstOrDefaultAsync(x => x.Id == bookingId);
            if (booking == null)
            {
                return new TechnicianAssignmentResult { Outcome = TechnicianAssignmentOutcome.BookingNotFound };
            }

            var currentName = booking.Technician != null
                ? NameFormat.Full(booking.Technician.FirstName, booking.Technician.LastName)
                : null;

            // Asked again here, not only by the page that shows the picker: the customer is
            // emailed a name and a number, and that must not happen for a booking that was
            // cancelled, or never paid for, while the admin's page sat open.
            if (!BookingRules.CanPickTechnician(booking.Status?.Name, booking.Source == BookingSource.Website, IsDepositPaid(booking)))
            {
                return new TechnicianAssignmentResult { Outcome = TechnicianAssignmentOutcome.NotAllowed, TechnicianName = currentName };
            }

            // A null id is the "-- Unassigned --" option and clears the assignment. Anything
            // else has to resolve to a real Technician row first: TechnicianId is a live FK,
            // so writing an unknown id (Guid.Empty being the easy way to get one from a form
            // post) fails at the database with a raw constraint violation, not a 400.
            Technician technician = null;
            if (technicianId.HasValue)
            {
                technician = await this.technicianRepository.All()
                    .FirstOrDefaultAsync(x => x.Id == technicianId.Value);
                if (technician == null)
                {
                    return new TechnicianAssignmentResult { Outcome = TechnicianAssignmentOutcome.TechnicianNotFound, TechnicianName = currentName };
                }
            }

            // Saving the form as it stands changes nothing, and must not email the customer a
            // second time with what they were already told.
            if (booking.TechnicianId == technicianId)
            {
                return new TechnicianAssignmentResult { Outcome = TechnicianAssignmentOutcome.Unchanged, TechnicianName = currentName };
            }

            booking.TechnicianId = technicianId;

            if (technician == null)
            {
                booking.History.Add(JobHistory.Line($"Technician taken off (was {currentName})."));
                await this.bookingRepository.SaveChangesAsync();
                return new TechnicianAssignmentResult { Outcome = TechnicianAssignmentOutcome.Cleared };
            }

            var newName = NameFormat.Full(technician.FirstName, technician.LastName);

            // Every job is emailed the technician, a job written in by the admin as well as a
            // website booking: from here on the two go the same way (Section 3dt). A job written
            // in may have no email address, and then the admin tells the customer who is coming.
            if (string.IsNullOrWhiteSpace(booking.Email))
            {
                booking.History.Add(JobHistory.Line($"Technician picked: {newName}. The job has no email address, so the customer was not emailed."));
                await this.bookingRepository.SaveChangesAsync();
                return new TechnicianAssignmentResult { Outcome = TechnicianAssignmentOutcome.AssignedNoEmailAddress, TechnicianName = newName };
            }

            // Saved before the email goes, so a send that fails cannot undo the assignment. The
            // history line follows the send, because it says whether the customer was told.
            await this.bookingRepository.SaveChangesAsync();

            var emailed = await this.SendTechnicianEmailAsync(booking, technician);

            booking.History.Add(JobHistory.Line(emailed
                ? $"Technician picked: {newName}. The customer was emailed."
                : $"Technician picked: {newName}. The email to the customer could not be sent."));
            if (!emailed)
            {
                // For the dashboard's list of emails that did not go (PROJECT_STATE.md Section 3cq).
                booking.EmailsNotSent.Add(new EmailNotSent { What = "The technician's name, to the customer" });
            }

            await this.bookingRepository.SaveChangesAsync();

            return new TechnicianAssignmentResult
            {
                Outcome = emailed
                    ? TechnicianAssignmentOutcome.AssignedAndCustomerEmailed
                    : TechnicianAssignmentOutcome.AssignedButEmailNotSent,
                TechnicianName = newName,
            };
        }

        public async Task<JobCancelOutcome> CancelBookingAsync(Guid bookingId, string reason)
        {
            Booking booking = await this.bookingRepository.All()
                .Include(x => x.Status)
                .Include(x => x.AvailabilitySlot)
                .FirstOrDefaultAsync(x => x.Id == bookingId);

            BookingStatus cancelledStatus = await this.statusRepository.All().FirstOrDefaultAsync(x => x.Name == BookingStatusNames.Cancelled);

            reason = (reason ?? string.Empty).Trim();

            if (booking == null
                || cancelledStatus == null
                || reason.Length == 0
                || !BookingRules.CanCancel(booking.Status?.Name))
            {
                return JobCancelOutcome.NotAllowed;
            }

            if (booking.Status?.Name == BookingStatusNames.Pending)
            {
                // A website booking still waiting for its deposit has a payment page open at
                // Stripe. It is closed first, or the customer could go on to pay for a booking
                // that has just been cancelled. If the page cannot be reached the job is
                // cancelled all the same; money that still comes for it is written on the job
                // and the company is told (PaymentsService.ProcessPaymentSuccessAsync).
                //
                // A page found paid means the customer paid in this same moment: the deposit is
                // written down, the booking confirmed and both emails sent. Nothing is cancelled
                // then. It used to be cancelled straight after, with the admin told the
                // customer had not been emailed; now the admin is told and decides.
                if (await this.paymentsService.CloseCheckoutsAsync(bookingId) == CheckoutClosure.Paid)
                {
                    return JobCancelOutcome.PaidJustNow;
                }
            }

            // The reason is kept on the job, with the day and hour it was for: its slot goes back
            // on sale below, and a slot given back no longer says whose it was.
            booking.StatusId = cancelledStatus.Id;
            booking.CancelReason = reason.Length > MaxCancelReasonLength ? reason.Substring(0, MaxCancelReasonLength) : reason;
            booking.ScheduledStart ??= booking.AvailabilitySlot?.StartTime;
            booking.ScheduledEnd ??= booking.AvailabilitySlot?.EndTime;

            // Release slot
            List<AvailabilitySlot> slots = await this.slotRepository.All().Where(x => x.BookingId == bookingId).ToListAsync();
            foreach (AvailabilitySlot slot in slots)
            {
                slot.IsBooked = false;
                slot.BookingId = null;
            }

            try
            {
                await this.bookingRepository.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException ex)
            {
                // A concurrent process (e.g. StaleBookingCleanupService's abandonment
                // sweep touching the same slot at the same moment an admin cancels it
                // here) bumped one of these slots' RowVersion between our read and our
                // write. Refresh the conflicting entries' original values to the current
                // database state so the retry's concurrency check succeeds, while
                // keeping our own intended "release this slot" values - the booking's
                // own status change has no concurrency token, so it can't be what
                // conflicted, and is safe to resend as-is.
                foreach (EntityEntry entry in ex.Entries)
                {
                    PropertyValues databaseValues = await entry.GetDatabaseValuesAsync();
                    if (databaseValues == null)
                    {
                        // The row is gone entirely (hard-deleted concurrently) - nothing
                        // left to reconcile for this entry.
                        entry.State = EntityState.Detached;
                    }
                    else
                    {
                        entry.OriginalValues.SetValues(databaseValues);
                    }
                }

                await this.bookingRepository.SaveChangesAsync();
            }

            // The history line goes in a save of its own, after the one above has landed. That
            // save can be sent twice (the retry just above), and a new row riding along with it
            // would be sent twice with it.
            booking.History.Add(JobHistory.Line($"Cancelled. Reason: {booking.CancelReason}"));
            await this.bookingRepository.SaveChangesAsync();

            return JobCancelOutcome.Cancelled;
        }

        public async Task<JobMoveResult> MoveBookingAsync(Guid bookingId, DateTime newStart)
        {
            Booking booking = await this.bookingRepository.All()
                .Include(x => x.Status)
                .Include(x => x.Payments).ThenInclude(p => p.Status)
                .Include(x => x.AvailabilitySlot)
                .FirstOrDefaultAsync(x => x.Id == bookingId);
            if (booking == null)
            {
                return new JobMoveResult { Outcome = JobMoveOutcome.BookingNotFound };
            }

            var cameFromWebsite = booking.Source == BookingSource.Website;
            if (!BookingRules.CanMove(booking.Status?.Name, cameFromWebsite, IsDepositPaid(booking)))
            {
                return new JobMoveResult { Outcome = JobMoveOutcome.NotAllowed };
            }

            DateTime? oldStart = booking.ScheduledStart ?? booking.AvailabilitySlot?.StartTime;
            DateTime? oldEnd = booking.ScheduledEnd ?? booking.AvailabilitySlot?.EndTime;
            if (oldStart == newStart)
            {
                return new JobMoveResult { Outcome = JobMoveOutcome.Unchanged, NewStart = newStart };
            }

            AvailabilitySlot oldSlot = await this.slotRepository.All().FirstOrDefaultAsync(x => x.BookingId == bookingId);
            AvailabilitySlot newSlot = await this.slotRepository.All().FirstOrDefaultAsync(x => x.StartTime == newStart);

            // A website booking carries its hour in the calendar with it: its deposit promised
            // the time, so the new hour comes off sale where the calendar has it free. A job that
            // was written in holds no hour, here as when it was made; the admin blocks by hand.
            var takeNewHour = cameFromWebsite && newSlot != null && !newSlot.IsBooked && !newSlot.IsBlocked;

            // The hour it leaves goes back on sale and the new one comes off together, or
            // neither does: a move that failed half way would sell the same hour twice.
            await using (IDbContextTransaction transaction = await this.dbQueryRunner.BeginTransactionAsync())
            {
                if (oldSlot != null && !await this.availabilityService.ReleaseSlotAsync(oldSlot.Id))
                {
                    await transaction.RollbackAsync();
                    return new JobMoveResult { Outcome = JobMoveOutcome.CalendarChanged };
                }

                if (takeNewHour && !await this.availabilityService.BookSlotAsync(newSlot.Id, bookingId))
                {
                    await transaction.RollbackAsync();
                    return new JobMoveResult { Outcome = JobMoveOutcome.CalendarChanged };
                }

                // It keeps the length it had, an hour unless its slot was longer.
                TimeSpan length = oldStart.HasValue && oldEnd.HasValue && oldEnd > oldStart ? oldEnd.Value - oldStart.Value : TimeSpan.FromHours(1);
                booking.ScheduledStart = newStart;
                booking.ScheduledEnd = newStart + length;
                booking.History.Add(JobHistory.Line(oldStart.HasValue
                    ? $"Moved from {JobHistory.DayAndHour(oldStart.Value)} to {JobHistory.DayAndHour(newStart)}."
                    : $"Moved to {JobHistory.DayAndHour(newStart)}."));
                await this.bookingRepository.SaveChangesAsync();

                await transaction.CommitAsync();
            }

            return new JobMoveResult
            {
                Outcome = JobMoveOutcome.Moved,
                NewStart = newStart,
                OldHourFreed = oldSlot != null,
                NewHourTaken = takeNewHour,
                NewHourNotAvailable = cameFromWebsite && !takeNewHour,
            };
        }

        public async Task<JobEditResult> EditDetailsAsync(JobEditInputModel model)
        {
            Booking booking = await this.bookingRepository.All()
                .Include(x => x.Status)
                .Include(x => x.Technician)
                .Include(x => x.BookingServices)
                .FirstOrDefaultAsync(x => x.Id == model.Id);
            if (booking == null)
            {
                return new JobEditResult { Outcome = JobEditOutcome.BookingNotFound };
            }

            // Asked again here, not only by the page that shows the button: a job cancelled
            // while the form sat open keeps the details it was cancelled with.
            if (!BookingRules.CanEditDetails(booking.Status?.Name))
            {
                return new JobEditResult { Outcome = JobEditOutcome.NotAllowed };
            }

            var firstName = NullIfBlank(model.CustomerFirstName);
            var lastName = NullIfBlank(model.CustomerLastName);
            var phoneNumber = NullIfBlank(model.PhoneNumber);
            var email = NullIfBlank(model.Email);
            var address = NullIfBlank(model.Address);
            var description = NullIfBlank(model.ProblemDescription);

            if (firstName == null || phoneNumber == null)
            {
                throw new InvalidOperationException("A job needs a first name and a phone number.");
            }

            // The site emails a website customer: when the deposit is paid and when a technician
            // is picked. That job keeps an address to email.
            var cameFromWebsite = booking.Source == BookingSource.Website;
            if (cameFromWebsite && email == null)
            {
                return new JobEditResult { Outcome = JobEditOutcome.EmailNeeded };
            }

            // A website booking stays one, whatever the form sends. A written-in job can be moved
            // between the other ways a job arrives and never to "Website": with no deposit and no
            // slot it would sit in the list looking like a paid booking's twin.
            BookingSource source = booking.Source;
            if (!cameFromWebsite)
            {
                if (model.Source == null || !JobDetailsInputModel.WrittenInSources.Contains(model.Source.Value))
                {
                    return new JobEditResult { Outcome = JobEditOutcome.SourceNeeded };
                }

                source = model.Source.Value;
            }

            // A job has one service or none. The one it has is compared by its id, so a job
            // booked with a service that was deleted since keeps it when the form is saved as
            // it stands, and nothing is looked up that the admin did not change. A line taken off
            // earlier is left out by name: the query does not load one, but a context that took
            // it off itself still holds it in the job's list.
            BookingService serviceLine = booking.BookingServices
                .Where(x => !x.IsDeleted)
                .OrderBy(x => x.CreatedOn)
                .FirstOrDefault();
            var serviceChanged = model.ServiceId != serviceLine?.ServiceId;
            Service newService = null;
            if (serviceChanged && model.ServiceId.HasValue)
            {
                newService = await this.serviceRepository.All().FirstOrDefaultAsync(x => x.Id == model.ServiceId.Value);
                if (newService == null)
                {
                    return new JobEditResult { Outcome = JobEditOutcome.ServiceNotFound };
                }
            }

            // What changed, twice over: in a word each for the line the admin is shown, and in a
            // sentence each, with what the box held before, for the job's history.
            var changed = new List<string>();
            var sentences = new List<string>();

            if (IsDifferent(booking.CustomerFirstName, firstName))
            {
                changed.Add("first name");
                sentences.Add(JobHistory.Was("First name", booking.CustomerFirstName, "empty"));
                booking.CustomerFirstName = firstName;
            }

            if (IsDifferent(booking.CustomerLastName, lastName))
            {
                changed.Add("last name");
                sentences.Add(JobHistory.Was("Last name", booking.CustomerLastName, "empty"));
                booking.CustomerLastName = lastName;
            }

            if (IsDifferent(booking.PhoneNumber, phoneNumber))
            {
                changed.Add("phone number");
                sentences.Add(JobHistory.Was("Phone number", booking.PhoneNumber, "empty"));
                booking.PhoneNumber = phoneNumber;
            }

            var emailChanged = IsDifferent(booking.Email, email);
            if (emailChanged)
            {
                changed.Add("email");
                sentences.Add(JobHistory.Was("Email", booking.Email, "empty"));
                booking.Email = email;
            }

            if (IsDifferent(booking.Address, address))
            {
                changed.Add("address");
                sentences.Add(JobHistory.Was("Address", booking.Address, "not written down"));
                booking.Address = address;
            }

            if (serviceChanged)
            {
                // Asked for among deleted services too, so the line can name the one it was.
                var oldServiceName = serviceLine == null
                    ? null
                    : await this.serviceRepository.AllWithDeleted()
                        .Where(x => x.Id == serviceLine.ServiceId)
                        .Select(x => x.Name)
                        .FirstOrDefaultAsync();

                changed.Add("service");
                sentences.Add(JobHistory.Was("Service", oldServiceName, "not picked"));

                if (newService == null)
                {
                    this.bookingServiceRepository.Delete(serviceLine);
                }
                else if (serviceLine == null)
                {
                    // Added through its own repository, not through the job's list: a new object
                    // found on a job that is already loaded, and arriving with a key, is taken
                    // for a row that exists and updated instead of inserted (BookingHistoryEntry
                    // has the same story).
                    await this.bookingServiceRepository.AddAsync(new BookingService
                    {
                        BookingId = booking.Id,
                        ServiceId = newService.Id,
                        PriceAtBooking = newService.BasePrice,
                        Quantity = 1,
                    });
                }
                else
                {
                    serviceLine.ServiceId = newService.Id;
                    serviceLine.PriceAtBooking = newService.BasePrice;
                }

                // The service's price is the job's estimate. What the job came to and what has
                // been paid are left alone.
                booking.TotalAmount = newService?.BasePrice;
            }

            if (booking.Source != source)
            {
                changed.Add("where it came from");
                sentences.Add($"Where it came from was {booking.Source}.");
                booking.Source = source;
            }

            HeardAbout heardAbout = HeardAboutText.Checked(model.HeardAbout);
            if (booking.HeardAbout != heardAbout)
            {
                changed.Add("how they heard about us");
                sentences.Add($"How they heard about us was: {HeardAboutText.Label(booking.HeardAbout)}.");
                booking.HeardAbout = heardAbout;
            }

            // Last, because it is the one box that can be long: it gets what is left of the line.
            if (IsDifferent(booking.ProblemDescription, description))
            {
                var room = JobHistory.LineRoom
                    - DetailsChangedPrefix.Length
                    - sentences.Sum(x => x.Length + 1)
                    - DescriptionSentence.Length;

                changed.Add("what the job is");
                sentences.Add(booking.ProblemDescription == null
                    ? "The job had no description."
                    : string.Format(DescriptionSentence, JobHistory.Shorten(booking.ProblemDescription, room)));
                booking.ProblemDescription = description;
            }

            if (changed.Count == 0)
            {
                return new JobEditResult { Outcome = JobEditOutcome.Unchanged };
            }

            booking.History.Add(JobHistory.Line(DetailsChangedPrefix + " " + string.Join(" ", sentences)));
            await this.bookingRepository.SaveChangesAsync();

            // Saving a new address sends nothing. The emails the site sent this customer before
            // went to the old one, and the admin's page says so.
            var emailChangedOnWebsiteBooking = cameFromWebsite && emailChanged;

            return new JobEditResult
            {
                Outcome = JobEditOutcome.Saved,
                Changed = changed,
                EmailChangedOnWebsiteBooking = emailChangedOnWebsiteBooking,
                TechnicianName = emailChangedOnWebsiteBooking && booking.Technician != null
                    ? NameFormat.Full(booking.Technician.FirstName, booking.Technician.LastName)
                    : null,
            };
        }

        public async Task<bool> SaveNotesAsync(Guid bookingId, string notes)
        {
            Booking booking = await this.bookingRepository.All().FirstOrDefaultAsync(x => x.Id == bookingId);
            if (booking == null)
            {
                return false;
            }

            // No history line: the notes are the admin's own, and the box holds the latest.
            booking.AdminNotes = NullIfBlank(notes);
            await this.bookingRepository.SaveChangesAsync();
            return true;
        }

        public async Task<IEnumerable<T>> GetHistoryAsync<T>(Guid bookingId)
        {
            return await this.bookingRepository.All()
                .Where(x => x.Id == bookingId)
                .SelectMany(x => x.History)
                .OrderBy(x => x.CreatedOn)
                .To<T>()
                .ToListAsync();
        }

        // The booking's payments have to be loaded with their statuses for this to say anything.
        private static bool IsDepositPaid(Booking booking)
        {
            return booking.Payments != null && booking.Payments.Any(p => p.Status != null && p.Status.Name == PaymentStatusNames.DepositPaid);
        }

        private static string NullIfBlank(string text)
        {
            return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        }

        private static bool IsAPrice(decimal amount)
        {
            return amount > 0m && amount <= 100000m;
        }

        // Whether the admin changed a box. An empty box and one never filled in are the same
        // thing, and so are two texts that differ only in the spaces around them or in how their
        // line breaks are written: the website keeps a description as the customer typed it,
        // spaces at the end and all, and a browser sends a text box back with line breaks of its
        // own. Compared letter for letter, a website booking's form saved as it stood counted
        // as a rewritten description and left a history line (PROJECT_STATE.md Section 3cf).
        private static bool IsDifferent(string held, string typed)
        {
            return !string.Equals(AsCompared(held), AsCompared(typed), StringComparison.Ordinal);
        }

        private static string AsCompared(string text)
        {
            return (text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Trim();
        }

        // The email's words are in BookingEmails.TechnicianPickedAsync. The assignment is saved by
        // now: a send that fails is logged and reported back, so the admin's page can say the
        // customer still has to be told.
        private async Task<bool> SendTechnicianEmailAsync(Booking booking, Technician technician)
        {
            // Asked for here, by the one caller that prints them: loaded with the booking above,
            // beside its payments, they would make that one query fetch two lists at once.
            List<string> serviceNames = await this.bookingRepository.All()
                .Where(x => x.Id == booking.Id)
                .SelectMany(x => x.BookingServices)
                .Select(x => x.Service.Name)
                .ToListAsync();

            return await this.bookingEmails.TechnicianPickedAsync(booking, technician, serviceNames);
        }
    }
}
