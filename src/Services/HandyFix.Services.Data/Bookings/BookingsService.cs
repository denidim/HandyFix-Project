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
        private readonly IDeletableEntityRepository<Booking> bookingRepository;
        private readonly IDeletableEntityRepository<Service> serviceRepository;
        private readonly IDeletableEntityRepository<AvailabilitySlot> slotRepository;
        private readonly IDeletableEntityRepository<BookingStatus> statusRepository;
        private readonly IDeletableEntityRepository<BookingImage> imageRepository;
        private readonly IDeletableEntityRepository<Technician> technicianRepository;
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
                BookingServices = new List<BookingService>(),
            };

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

            if (!string.IsNullOrWhiteSpace(statusFilter))
            {
                query = query.Where(x => x.Status.Name == statusFilter);
            }

            query = sortField switch
            {
                BookingSortField.AppointmentTime => descending
                    ? query.OrderByDescending(x => x.AvailabilitySlot.StartTime)
                    : query.OrderBy(x => x.AvailabilitySlot.StartTime),
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

        public async Task<bool> CompleteBookingAsync(Guid bookingId)
        {
            Booking booking = await this.bookingRepository.All()
                .Include(x => x.Status)
                .Include(x => x.Payments).ThenInclude(p => p.Status)
                .FirstOrDefaultAsync(x => x.Id == bookingId);
            BookingStatus completedStatus = await this.statusRepository.All().FirstOrDefaultAsync(x => x.Name == "Completed");

            if (booking == null
                || completedStatus == null
                || !BookingRules.CanComplete(booking.Status?.Name, IsDepositPaid(booking)))
            {
                return false;
            }

            booking.StatusId = completedStatus.Id;
            await this.bookingRepository.SaveChangesAsync();
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
            if (!BookingRules.CanPickTechnician(booking.Status?.Name, IsDepositPaid(booking)))
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
            await this.bookingRepository.SaveChangesAsync();

            if (technician == null)
            {
                return new TechnicianAssignmentResult { Outcome = TechnicianAssignmentOutcome.Cleared };
            }

            var emailed = await this.SendTechnicianEmailAsync(booking, technician);

            return new TechnicianAssignmentResult
            {
                Outcome = emailed
                    ? TechnicianAssignmentOutcome.AssignedAndCustomerEmailed
                    : TechnicianAssignmentOutcome.AssignedButEmailNotSent,
                TechnicianName = NameFormat.Full(technician.FirstName, technician.LastName),
            };
        }

        public async Task<bool> CancelBookingAsync(Guid bookingId)
        {
            Booking booking = await this.bookingRepository.All()
                .Include(x => x.Status)
                .Include(x => x.AvailabilitySlot)
                .FirstOrDefaultAsync(x => x.Id == bookingId);

            BookingStatus cancelledStatus = await this.statusRepository.All().FirstOrDefaultAsync(x => x.Name == "Cancelled");

            if (booking == null
                || cancelledStatus == null
                || !BookingRules.CanCancel(booking.Status?.Name))
            {
                return false;
            }

            booking.StatusId = cancelledStatus.Id;

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
                await transaction.CommitAsync();
            }
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
            }

            foreach (AvailabilitySlot slot in slotsToRelease)
            {
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
            return new BookingSummaryStats
            {
                TodaysAppointmentsCount = bookings.Count(b => b.ScheduledTime.Date == DateTime.Today),

                // The admin's to-do after a deposit comes in: paid bookings nobody is on yet.
                AwaitingTechnicianCount = bookings.Count(b => b.CanPickTechnician && b.TechnicianId == null),
                MonthlyRevenue = bookings.Any()
                    ? bookings.Where(b => b.ScheduledTime.Month == DateTime.Today.Month && b.ScheduledTime.Year == DateTime.Today.Year).Sum(b => b.TotalAmount)
                    : 0,
            };
        }

        public async Task<IEnumerable<string>> GetStatusOptionsAsync()
        {
            return await this.statusRepository.All()
                .Select(x => x.Name)
                .OrderBy(x => x)
                .ToListAsync();
        }

        // The booking's payments have to be loaded with their statuses for this to say anything.
        private static bool IsDepositPaid(Booking booking)
        {
            return booking.Payments != null && booking.Payments.Any(p => p.Status != null && p.Status.Name == "DepositPaid");
        }

        // The one customer email that names the technician, sent when an admin picks one. It used
        // to hang off the "Approve" button, which a paid booking never showed, so no customer was
        // ever sent it (PROJECT_STATE.md Section 3ce). The assignment is saved by now: a send that
        // fails is logged and reported back, so the admin's page can say the customer still has
        // to be told.
        private async Task<bool> SendTechnicianEmailAsync(Booking booking, Technician technician)
        {
            var technicianName = EmailText.Encode(NameFormat.Full(technician.FirstName, technician.LastName));
            var visit = booking.AvailabilitySlot != null
                ? $" on <strong>{booking.AvailabilitySlot.StartTime:dd MMM yyyy 'at' HH:mm}</strong>"
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
