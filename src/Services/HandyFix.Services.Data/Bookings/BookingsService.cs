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

            // Slot must exist before we do any work; its StartTime is needed below, and the
            // actual availability check happens atomically in BookSlotAsync.
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

            // 5. Tell the customer the booking was received. The booking and its slot are saved by
            // now, so a send that fails is logged and the customer goes on to pay: an exception
            // here used to show "an error occurred while saving your booking" for a booking that
            // had been saved, and a second try then lost the slot to the first one.
            // What the customer typed is encoded before it goes into the email's HTML.
            var subject = "Your Plumbing Handyman Surrey Booking Inquiry has been Received!";
            var body = $@"
            <h3>Hello {EmailText.Encode(model.CustomerFirstName)} {EmailText.Encode(model.CustomerLastName)},</h3>
            <p>Thank you for choosing <strong>Plumbing Handyman Surrey</strong>. We have received your booking request details:</p>
            <ul>
                <li><strong>Booking Reference:</strong> {booking.Id}</li>
                <li><strong>Service(s):</strong> {EmailText.Encode(string.Join(", ", selectedServices.Select(s => s.Name)))}</li>
                <li><strong>Scheduled Time:</strong> {slot.StartTime:dd MMM yyyy 'at' HH:mm}</li>
                <li><strong>Address:</strong> {EmailText.Encode(address)}</li>
            </ul>
            <p>To secure this appointment slot, please pay the deposit of £{depositAmount.ToString("F2")} on the next screen.</p>
            <p>Once paid, we will confirm your technician assignment.</p>
            <br />
            <p>Best Regards,<br/><strong>The Plumbing Handyman Surrey Team</strong></p>";

            await this.emailSender.TrySendEmailAsync(
                this.logger,
                "booking received, to the customer",
                EmailSettings.SystemFromAddress(this.configuration),
                EmailSettings.CustomerFromName,
                model.Email,
                subject,
                body);

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

        public async Task UpdateStatusAsync(Guid bookingId, string statusName)
        {
            Booking booking = await this.bookingRepository.All()
                .Include(x => x.Technician)
                .FirstOrDefaultAsync(x => x.Id == bookingId);
            BookingStatus status = await this.statusRepository.All().FirstOrDefaultAsync(x => x.Name.ToLower() == statusName.ToLower());

            if (booking != null && status != null)
            {
                booking.StatusId = status.Id;
                await this.bookingRepository.SaveChangesAsync();

                if (statusName == "Approved")
                {
                    // This is the one customer email that names the technician - by the time an
                    // admin approves, the assignment has been made. The deposit confirmation
                    // deliberately says nothing about it (see PaymentsService).
                    var technicianBlock = booking.Technician != null
                        ? $@"<p>Your technician for this visit is <strong>{EmailText.Encode(NameFormat.Full(booking.Technician.FirstName, booking.Technician.LastName))}</strong>
                             (<a href=""tel:{EmailText.Encode(booking.Technician.PhoneNumber)}"">{EmailText.Encode(booking.Technician.PhoneNumber)}</a>).</p>"
                        : "<p>A professional technician is scheduled for your address at the selected slot.</p>";

                    // Send Booking Confirmed email
                    var subject = "Your Plumbing Handyman Surrey Booking is CONFIRMED!";
                    var body = $@"
                        <h3>Hi {EmailText.Encode(booking.CustomerFirstName)},</h3>
                        <p>We are pleased to inform you that your booking reference <strong>{booking.Id}</strong> is officially confirmed.</p>
                        {technicianBlock}
                        <p>Thank you for choosing Plumbing Handyman Surrey!</p>";

                    // The status is saved by now. A send that fails is logged, not thrown: the
                    // admin's page would otherwise fail on a booking that had in fact been approved.
                    await this.emailSender.TrySendEmailAsync(
                        this.logger,
                        "booking confirmed, to the customer",
                        EmailSettings.BookingsFromAddress(this.configuration),
                        EmailSettings.CustomerFromName,
                        booking.Email,
                        subject,
                        body);
                }
            }
        }

        public async Task AssignTechnicianAsync(Guid bookingId, Guid? technicianId)
        {
            Booking booking = await this.bookingRepository.All().FirstOrDefaultAsync(x => x.Id == bookingId);
            if (booking == null)
            {
                return;
            }

            // A null id is the "-- Unassigned --" option and clears the assignment. Anything
            // else has to resolve to a real Technician row first: TechnicianId is a live FK,
            // so writing an unknown id (Guid.Empty being the easy way to get one from a form
            // post) fails at the database with a raw constraint violation, not a 400.
            if (technicianId.HasValue)
            {
                var exists = await this.technicianRepository.All()
                    .AnyAsync(x => x.Id == technicianId.Value);
                if (!exists)
                {
                    return;
                }
            }

            booking.TechnicianId = technicianId;
            await this.bookingRepository.SaveChangesAsync();
        }

        public async Task CancelBookingAsync(Guid bookingId)
        {
            Booking booking = await this.bookingRepository.All()
                .Include(x => x.AvailabilitySlot)
                .FirstOrDefaultAsync(x => x.Id == bookingId);

            BookingStatus cancelledStatus = await this.statusRepository.All().FirstOrDefaultAsync(x => x.Name == "Cancelled");

            if (booking == null || cancelledStatus == null)
            {
                return;
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
                PendingApprovalCount = bookings.Count(b => b.StatusName == "Pending"),
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
    }
}
