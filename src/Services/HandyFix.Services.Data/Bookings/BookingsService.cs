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
    using HandyFix.Services.Data.Payments;
    using HandyFix.Services.Mapping;
    using HandyFix.Services.Messaging;
    using HandyFix.Web.ViewModels.Booking;
    using HandyFix.Web.ViewModels.Validation;

    using Mapster;

    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.ChangeTracking;
    using Microsoft.EntityFrameworkCore.Storage;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;

    public class BookingsService : IBookingsService
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
        private readonly IDeletableEntityRepository<BookingImage> imageRepository;
        private readonly IDeletableEntityRepository<Technician> technicianRepository;
        private readonly IDeletableEntityRepository<BookingService> bookingServiceRepository;
        private readonly IAvailabilityService availabilityService;
        private readonly IPaymentsService paymentsService;
        private readonly IDbQueryRunner dbQueryRunner;
        private readonly IEmailSender emailSender;
        private readonly IConfiguration configuration;
        private readonly ILogger<BookingsService> logger;

        public BookingsService(
            IDeletableEntityRepository<Booking> bookingRepository,
            IDeletableEntityRepository<Service> serviceRepository,
            IDeletableEntityRepository<AvailabilitySlot> slotRepository,
            IDeletableEntityRepository<BookingStatus> statusRepository,
            IDeletableEntityRepository<BookingImage> imageRepository,
            IDeletableEntityRepository<Technician> technicianRepository,
            IDeletableEntityRepository<BookingService> bookingServiceRepository,
            IAvailabilityService availabilityService,
            IPaymentsService paymentsService,
            IDbQueryRunner dbQueryRunner,
            IEmailSender emailSender,
            IConfiguration configuration,
            ILogger<BookingsService> logger)
        {
            this.bookingRepository = bookingRepository;
            this.serviceRepository = serviceRepository;
            this.slotRepository = slotRepository;
            this.statusRepository = statusRepository;
            this.imageRepository = imageRepository;
            this.technicianRepository = technicianRepository;
            this.bookingServiceRepository = bookingServiceRepository;
            this.availabilityService = availabilityService;
            this.paymentsService = paymentsService;
            this.dbQueryRunner = dbQueryRunner;
            this.emailSender = emailSender;
            this.configuration = configuration;
            this.logger = logger;
        }

        // The form takes the postcode in a box of its own so it can be checked. A booking has one
        // address, so the two are joined here, with the postcode written the standard way
        // ("KT9 2QN"). A visitor who typed the postcode into the address as well gets it once.
        public static string JoinAddressAndPostcode(string address, string postcode)
        {
            var street = (address ?? string.Empty).Trim().TrimEnd(',').TrimEnd();
            var normalized = UkPostcode.Normalize(postcode);
            if (normalized == null)
            {
                return street;
            }

            var streetCompact = new string(street.Where(c => !char.IsWhiteSpace(c)).ToArray());
            var postcodeCompact = normalized.Replace(" ", string.Empty);

            return streetCompact.Contains(postcodeCompact, StringComparison.OrdinalIgnoreCase)
                ? street
                : $"{street}, {normalized}";
        }

        public async Task<Booking> CreateBookingAsync(
            BookingInputModel model,
            IReadOnlyList<string> imageUrls,
            string userId = null)
        {
            BookingStatus pendingStatus = await this.statusRepository.All().FirstOrDefaultAsync(x => x.Name == "Pending");
            if (pendingStatus == null)
            {
                throw new InvalidOperationException("Booking status 'Pending' is not seeded.");
            }

            // Slot must exist before we do any work; the actual availability check happens
            // atomically in BookSlotAsync.
            AvailabilitySlot slot = await this.slotRepository.All().FirstOrDefaultAsync(x => x.Id == model.SlotId);
            if (slot == null)
            {
                throw new InvalidOperationException("The selected time slot does not exist. Please choose a different time.");
            }

            Guid[] serviceIds = new[] { model.ServiceId };
            List<Service> selectedServices = await this.serviceRepository.All()
                .Where(x => serviceIds.Contains(x.Id))
                .ToListAsync();

            var totalAmount = selectedServices.Sum(x => x.BasePrice);
            var depositAmount = 50.00m; // Flat booking deposit
            var address = JoinAddressAndPostcode(model.Address, model.Postcode);

            // 1. Build the booking object framework completely in-memory
            var booking = new Booking
            {
                CustomerFirstName = model.CustomerFirstName,
                CustomerLastName = model.CustomerLastName,
                Email = model.Email,
                PhoneNumber = model.PhoneNumber,
                Address = address,
                ProblemDescription = model.ProblemDescription,
                StatusId = pendingStatus.Id,
                UserId = userId,
                TotalAmount = totalAmount,
                DepositAmount = depositAmount,
                Source = BookingSource.Website,

                // The job's own copy of its day and hour. It keeps them when it gives the slot
                // back, so a cancelled or abandoned booking still says when it was for.
                ScheduledStart = slot.StartTime,
                ScheduledEnd = slot.EndTime,
                BookingServices = new List<BookingService>(),
            };

            booking.History.Add(JobHistory.Line("Booked on the website."));

            // 2. Link services directly to the navigation property list before saving
            foreach (Service svc in selectedServices)
            {
                var bookingService = new BookingService
                {
                    ServiceId = svc.Id,
                    PriceAtBooking = svc.BasePrice,
                    Quantity = 1,
                };
                booking.BookingServices.Add(bookingService);
            }

            // 3. Create the booking and claim the slot atomically: if the slot was taken
            // or blocked in the meantime, roll back the booking instead of leaving an
            // orphaned booking with no appointment time.
            await using (IDbContextTransaction transaction = await this.dbQueryRunner.BeginTransactionAsync())
            {
                await this.bookingRepository.AddAsync(booking);
                await this.bookingRepository.SaveChangesAsync();

                var slotBooked = await this.availabilityService.BookSlotAsync(model.SlotId, booking.Id);
                if (!slotBooked)
                {
                    await transaction.RollbackAsync();
                    throw new SlotUnavailableException("The selected time slot is no longer available. Please choose a different time.");
                }

                await transaction.CommitAsync();
            }

            // 4. Persist uploaded image URLs as BookingImage records
            if (imageUrls != null && imageUrls.Count > 0)
            {
                foreach (var url in imageUrls)
                {
                    var image = new BookingImage
                    {
                        BookingId = booking.Id,
                        ImageUrl = url,
                    };
                    await this.imageRepository.AddAsync(image);
                }

                await this.imageRepository.SaveChangesAsync();
            }

            // No email goes out here, on purpose. A booking that is never paid is abandoned after
            // about fifteen minutes and its slot goes back on sale, so "we have received your
            // booking" promised something that could be gone before it was read. The first email a
            // customer gets is the one that follows the deposit (PaymentsService;
            // PROJECT_STATE.md Section 3ce).
            return booking;
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
            BookingStatus bookedStatus = await this.statusRepository.All().FirstOrDefaultAsync(x => x.Name == "Approved");
            if (bookedStatus == null)
            {
                throw new InvalidOperationException("Booking status 'Approved' is not seeded.");
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

        public async Task<T> GetByIdAsync<T>(Guid id)
        {
            return await this.bookingRepository.All()
                .Where(x => x.Id == id)
                .To<T>()
                .FirstOrDefaultAsync();
        }

        public async Task<IEnumerable<T>> GetAllBookingsAsync<T>(
            BookingSortField sortField = BookingSortField.CreatedOn,
            bool descending = true,
            string statusFilter = null)
        {
            IQueryable<Booking> query = this.bookingRepository.All();

            // The filter is one of the job's labels ("Booked", "Done"), which may stand for more
            // than one of the database's own status names (JobLabels).
            if (!string.IsNullOrWhiteSpace(statusFilter))
            {
                IReadOnlyList<string> statusNames = JobLabels.StatusNames(statusFilter);
                query = query.Where(x => statusNames.Contains(x.Status.Name));
            }

            query = sortField switch
            {
                BookingSortField.AppointmentTime => descending
                    ? query.OrderByDescending(x => x.ScheduledStart)
                    : query.OrderBy(x => x.ScheduledStart),
                BookingSortField.CustomerName => descending
                    ? query.OrderByDescending(x => x.CustomerFirstName).ThenByDescending(x => x.CustomerLastName)
                    : query.OrderBy(x => x.CustomerFirstName).ThenBy(x => x.CustomerLastName),
                BookingSortField.Status => descending
                    ? query.OrderByDescending(x => x.Status.Name)
                    : query.OrderBy(x => x.Status.Name),
                _ => descending
                    ? query.OrderByDescending(x => x.CreatedOn)
                    : query.OrderBy(x => x.CreatedOn),
            };

            return await query.To<T>().ToListAsync();
        }

        public async Task<IEnumerable<T>> GetUserBookingsAsync<T>(string userId)
        {
            return await this.bookingRepository.All()
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.CreatedOn)
                .To<T>()
                .ToListAsync();
        }

        public async Task<bool> CompleteBookingAsync(Guid bookingId, decimal finalPrice)
        {
            Booking booking = await this.bookingRepository.All()
                .Include(x => x.Status)
                .Include(x => x.Payments).ThenInclude(p => p.Status)
                .FirstOrDefaultAsync(x => x.Id == bookingId);
            BookingStatus completedStatus = await this.statusRepository.All().FirstOrDefaultAsync(x => x.Name == "Completed");

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

            // The site emails website customers only. For a job that was written in the admin is
            // already speaking to the customer, and tells them who is coming.
            if (booking.Source != BookingSource.Website)
            {
                booking.History.Add(JobHistory.Line($"Technician picked: {newName}."));
                await this.bookingRepository.SaveChangesAsync();
                return new TechnicianAssignmentResult { Outcome = TechnicianAssignmentOutcome.AssignedNoEmailForWrittenInJob, TechnicianName = newName };
            }

            // Saved before the email goes, so a send that fails cannot undo the assignment. The
            // history line follows the send, because it says whether the customer was told.
            await this.bookingRepository.SaveChangesAsync();

            var emailed = await this.SendTechnicianEmailAsync(booking, technician);

            booking.History.Add(JobHistory.Line(emailed
                ? $"Technician picked: {newName}. The customer was emailed."
                : $"Technician picked: {newName}. The email to the customer could not be sent."));
            await this.bookingRepository.SaveChangesAsync();

            return new TechnicianAssignmentResult
            {
                Outcome = emailed
                    ? TechnicianAssignmentOutcome.AssignedAndCustomerEmailed
                    : TechnicianAssignmentOutcome.AssignedButEmailNotSent,
                TechnicianName = newName,
            };
        }

        public async Task<bool> CancelBookingAsync(Guid bookingId, string reason)
        {
            Booking booking = await this.bookingRepository.All()
                .Include(x => x.Status)
                .Include(x => x.AvailabilitySlot)
                .FirstOrDefaultAsync(x => x.Id == bookingId);

            BookingStatus cancelledStatus = await this.statusRepository.All().FirstOrDefaultAsync(x => x.Name == "Cancelled");

            reason = (reason ?? string.Empty).Trim();

            if (booking == null
                || cancelledStatus == null
                || reason.Length == 0
                || !BookingRules.CanCancel(booking.Status?.Name))
            {
                return false;
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

            return true;
        }

        public async Task RescheduleBookingAsync(Guid bookingId, Guid newSlotId)
        {
            Booking booking = await this.bookingRepository.All().FirstOrDefaultAsync(x => x.Id == bookingId);
            if (booking == null)
            {
                throw new InvalidOperationException("The booking does not exist.");
            }

            AvailabilitySlot newSlot = await this.slotRepository.All().FirstOrDefaultAsync(x => x.Id == newSlotId);
            if (newSlot == null)
            {
                throw new InvalidOperationException("The selected time slot does not exist. Please choose a different time.");
            }

            AvailabilitySlot oldSlot = await this.slotRepository.All().FirstOrDefaultAsync(x => x.BookingId == bookingId);

            // Release the old slot and claim the new one atomically: if the new slot was
            // taken, blocked, or lost a concurrency race, roll back so the booking stays
            // exactly where it was instead of ending up with no appointment time at all.
            await using (IDbContextTransaction transaction = await this.dbQueryRunner.BeginTransactionAsync())
            {
                if (oldSlot != null)
                {
                    var oldSlotReleased = await this.availabilityService.ReleaseSlotAsync(oldSlot.Id);
                    if (!oldSlotReleased)
                    {
                        await transaction.RollbackAsync();
                        throw new SlotUnavailableException("Unable to reschedule this booking right now. Please try again.");
                    }
                }

                var newSlotBooked = await this.availabilityService.BookSlotAsync(newSlotId, bookingId);
                if (!newSlotBooked)
                {
                    await transaction.RollbackAsync();
                    throw new SlotUnavailableException("The selected time slot is no longer available. Please choose a different time.");
                }

                // The booking's own technician is deliberately left untouched. Slots carry no
                // technician (see AvailabilitySlot) - moving a job to a different hour doesn't
                // change who was assigned to it, so an admin's assignment survives a reschedule.
                booking.ScheduledStart = newSlot.StartTime;
                booking.ScheduledEnd = newSlot.EndTime;
                await this.bookingRepository.SaveChangesAsync();

                await transaction.CommitAsync();
            }
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

        public async Task<IEnumerable<T>> GetJobsForDayAsync<T>(DateTime day)
        {
            DateTime from = day.Date;
            DateTime to = from.AddDays(1);

            // Every job that is on or done that day, written-in ones too. A cancelled or
            // abandoned job is left out: the admin opens the day to see who is going where.
            return await this.bookingRepository.All()
                .Where(x => x.ScheduledStart >= from && x.ScheduledStart < to)
                .Where(x => x.Status.Name != "Cancelled" && x.Status.Name != "Abandoned")
                .OrderBy(x => x.ScheduledStart)
                .To<T>()
                .ToListAsync();
        }

        public async Task<int> ReleaseAbandonedBookingsAsync(TimeSpan olderThan)
        {
            BookingStatus pendingStatus = await this.statusRepository.All().FirstOrDefaultAsync(x => x.Name == "Pending");
            BookingStatus abandonedStatus = await this.statusRepository.All().FirstOrDefaultAsync(x => x.Name == "Abandoned");
            if (pendingStatus == null || abandonedStatus == null)
            {
                return 0;
            }

            DateTime cutoff = DateTime.UtcNow - olderThan;
            List<Booking> staleBookings = await this.bookingRepository.All()
                .Where(x => x.StatusId == pendingStatus.Id && x.CreatedOn < cutoff)
                .ToListAsync();

            if (staleBookings.Count == 0)
            {
                return 0;
            }

            var staleBookingIds = staleBookings.Select(x => x.Id).ToList();
            List<AvailabilitySlot> slotsToRelease = await this.slotRepository.All()
                .Where(x => x.BookingId != null && staleBookingIds.Contains(x.BookingId.Value))
                .ToListAsync();

            foreach (Booking booking in staleBookings)
            {
                booking.StatusId = abandonedStatus.Id;
                booking.History.Add(JobHistory.Line("Abandoned: the deposit was not paid in time. Its hour went back on sale."));
            }

            foreach (AvailabilitySlot slot in slotsToRelease)
            {
                // A booking saved before jobs kept their own date takes it from the slot now,
                // in the last moment the slot still says whose it was.
                Booking owner = staleBookings.First(x => x.Id == slot.BookingId);
                owner.ScheduledStart ??= slot.StartTime;
                owner.ScheduledEnd ??= slot.EndTime;

                slot.IsBooked = false;
                slot.BookingId = null;
            }

            // Keep the booking/slot release and the payment cleanup consistent: either
            // both land together, or neither does.
            await using (IDbContextTransaction transaction = await this.dbQueryRunner.BeginTransactionAsync())
            {
                await this.bookingRepository.SaveChangesAsync();
                await this.paymentsService.CancelPendingPaymentsForBookingsAsync(staleBookingIds);

                await transaction.CommitAsync();
            }

            return staleBookings.Count;
        }

        public async Task AddBookingImageAsync(Guid bookingId, string imageUrl)
        {
            var image = new BookingImage
            {
                BookingId = bookingId,
                ImageUrl = imageUrl,
            };

            await this.imageRepository.AddAsync(image);
            await this.imageRepository.SaveChangesAsync();
        }

        public async Task<int> GetTotalCountAsync()
        {
            return await this.bookingRepository.All().CountAsync();
        }

        public async Task<int> GetPendingCountAsync()
        {
            return await this.bookingRepository.All().CountAsync(x => x.Status.Name == "Pending");
        }

        public BookingSummaryStats GetSummaryStats(IEnumerable<BookingDetailsViewModel> bookings)
        {
            // Deliberately takes the list rather than fetching it: the caller decides whether it
            // already has the right (unfiltered) list in hand or needs to fetch one, so a request
            // that isn't filtered doesn't pay for a redundant round trip.
            // A cancelled or abandoned job keeps its date now, so it has to be left out by name:
            // while it lost its date with its slot it fell out of both figures by accident.
            List<BookingDetailsViewModel> standing = bookings
                .Where(b => b.JobLabel == JobLabels.Booked || b.JobLabel == JobLabels.Done)
                .ToList();

            return new BookingSummaryStats
            {
                TodaysAppointmentsCount = standing.Count(b => b.ScheduledTime.Date == DateTime.Today),

                // The admin's to-do: jobs that are on and have nobody on them yet.
                AwaitingTechnicianCount = bookings.Count(b => b.CanPickTechnician && b.TechnicianId == null),

                // The final price where a job has one, its estimate until then.
                MonthlyRevenue = standing
                    .Where(b => b.ScheduledTime.Month == DateTime.Today.Month && b.ScheduledTime.Year == DateTime.Today.Year)
                    .Sum(b => b.Price),
            };
        }

        // The list's filter offers the job's labels, in the order a job goes through them.
        public Task<IEnumerable<string>> GetStatusOptionsAsync()
        {
            return Task.FromResult<IEnumerable<string>>(JobLabels.JobOptions);
        }

        // The booking's payments have to be loaded with their statuses for this to say anything.
        private static bool IsDepositPaid(Booking booking)
        {
            return booking.Payments != null && booking.Payments.Any(p => p.Status != null && p.Status.Name == "DepositPaid");
        }

        private static string NullIfBlank(string text)
        {
            return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        }

        private static bool IsAPrice(decimal amount)
        {
            return amount > 0m && amount <= 100000m;
        }

        // An empty box and a box never filled in are the same thing to the admin.
        private static bool IsDifferent(string held, string typed)
        {
            return !string.Equals(held ?? string.Empty, typed ?? string.Empty, StringComparison.Ordinal);
        }

        // The one customer email that names the technician, sent when an admin picks one. It used
        // to hang off the "Approve" button, which a paid booking never showed, so no customer was
        // ever sent it (PROJECT_STATE.md Section 3ce). The assignment is saved by now: a send that
        // fails is logged and reported back, so the admin's page can say the customer still has
        // to be told.
        private async Task<bool> SendTechnicianEmailAsync(Booking booking, Technician technician)
        {
            var technicianName = EmailText.Encode(NameFormat.Full(technician.FirstName, technician.LastName));
            DateTime? start = booking.ScheduledStart ?? booking.AvailabilitySlot?.StartTime;
            var visit = start.HasValue
                ? $" on <strong>{start.Value:dd MMM yyyy 'at' HH:mm}</strong>"
                : string.Empty;
            var phoneLine = !string.IsNullOrWhiteSpace(technician.PhoneNumber)
                ? $@"<p>You can reach {technicianName} on <a href=""tel:{EmailText.PhoneLink(technician.PhoneNumber)}"">{EmailText.Encode(technician.PhoneNumber)}</a>.</p>"
                : string.Empty;

            // Asked for here, by the one caller that prints them: loaded with the booking above,
            // beside its payments, they would make that one query fetch two lists at once.
            List<string> serviceNames = await this.bookingRepository.All()
                .Where(x => x.Id == booking.Id)
                .SelectMany(x => x.BookingServices)
                .Select(x => x.Service.Name)
                .ToListAsync();

            var subject = "Your technician for your Plumbing Handyman Surrey booking";
            var body = $@"
                <h3>Hi {EmailText.Encode(booking.CustomerFirstName)},</h3>
                <p>Your technician for your visit{visit} is <strong>{technicianName}</strong>.</p>
                {phoneLine}
                <ul>
                    <li><strong>Booking Reference:</strong> {BookingReference.Short(booking.Id)}</li>
                    <li><strong>Service(s):</strong> {EmailText.Encode(string.Join(", ", serviceNames))}</li>
                    <li><strong>Address:</strong> {EmailText.Encode(booking.Address)}</li>
                </ul>
                <p>Thank you for choosing Plumbing Handyman Surrey!</p>";

            return await this.emailSender.TrySendEmailAsync(
                this.logger,
                "technician picked, to the customer",
                EmailSettings.BookingsFromAddress(this.configuration),
                EmailSettings.CustomerFromName,
                booking.Email,
                subject,
                body);
        }
    }
}
