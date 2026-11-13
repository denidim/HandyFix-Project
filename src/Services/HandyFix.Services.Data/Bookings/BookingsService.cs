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
    using HandyFix.Web.ViewModels.Booking;
    using HandyFix.Web.ViewModels.Validation;

    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Storage;

    public class BookingsService : IBookingsService
    {
        private readonly IDeletableEntityRepository<Booking> bookingRepository;
        private readonly IDeletableEntityRepository<Service> serviceRepository;
        private readonly IDeletableEntityRepository<AvailabilitySlot> slotRepository;
        private readonly IDeletableEntityRepository<BookingStatus> statusRepository;
        private readonly IDeletableEntityRepository<BookingImage> imageRepository;
        private readonly IAvailabilityService availabilityService;
        private readonly IPaymentsService paymentsService;
        private readonly IDbQueryRunner dbQueryRunner;

        public BookingsService(
            IDeletableEntityRepository<Booking> bookingRepository,
            IDeletableEntityRepository<Service> serviceRepository,
            IDeletableEntityRepository<AvailabilitySlot> slotRepository,
            IDeletableEntityRepository<BookingStatus> statusRepository,
            IDeletableEntityRepository<BookingImage> imageRepository,
            IAvailabilityService availabilityService,
            IPaymentsService paymentsService,
            IDbQueryRunner dbQueryRunner)
        {
            this.bookingRepository = bookingRepository;
            this.serviceRepository = serviceRepository;
            this.slotRepository = slotRepository;
            this.statusRepository = statusRepository;
            this.imageRepository = imageRepository;
            this.availabilityService = availabilityService;
            this.paymentsService = paymentsService;
            this.dbQueryRunner = dbQueryRunner;
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

        public async Task<BookingCreateResult> CreateBookingAsync(
            BookingInputModel model,
            IReadOnlyList<string> imageUrls)
        {
            // A broken install, not the customer's problem: it stays an exception, and the
            // booking page logs it and shows its general message.
            BookingStatus pendingStatus = await this.statusRepository.All().FirstOrDefaultAsync(x => x.Name == BookingStatusNames.Pending);
            if (pendingStatus == null)
            {
                throw new InvalidOperationException($"Booking status '{BookingStatusNames.Pending}' is not seeded.");
            }

            // Slot must exist before we do any work; the actual availability check happens
            // atomically in BookSlotAsync.
            AvailabilitySlot slot = await this.slotRepository.All().FirstOrDefaultAsync(x => x.Id == model.SlotId);
            if (slot == null)
            {
                return new BookingCreateResult { Outcome = BookingCreateOutcome.SlotNotFound };
            }

            Guid[] serviceIds = new[] { model.ServiceId };
            List<Service> selectedServices = await this.serviceRepository.All()
                .Where(x => serviceIds.Contains(x.Id))
                .ToListAsync();

            var totalAmount = selectedServices.Sum(x => x.BasePrice);
            var depositAmount = Deposit.Amount;
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
                TotalAmount = totalAmount,
                DepositAmount = depositAmount,
                Source = BookingSource.Website,
                HeardAbout = HeardAboutText.Checked(model.HeardAbout),

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

                // The photos go into the booking's own save (the repositories share one
                // context), so they are written with it or not at all. They used to be saved
                // after the commit: a photo save that failed left the booking holding its slot
                // while the customer was told to try again, and the retry found the slot taken
                // by their own booking.
                if (imageUrls != null)
                {
                    foreach (var url in imageUrls)
                    {
                        await this.imageRepository.AddAsync(new BookingImage
                        {
                            BookingId = booking.Id,
                            ImageUrl = url,
                        });
                    }
                }

                await this.bookingRepository.SaveChangesAsync();

                var slotBooked = await this.availabilityService.BookSlotAsync(model.SlotId, booking.Id);
                if (!slotBooked)
                {
                    await transaction.RollbackAsync();
                    return new BookingCreateResult { Outcome = BookingCreateOutcome.SlotTaken };
                }

                await transaction.CommitAsync();
            }

            // No email goes out here, on purpose. A booking that is never paid is abandoned after
            // about fifteen minutes and its slot goes back on sale, so "we have received your
            // booking" promised something that could be gone before it was read. The first email a
            // customer gets is the one that follows the deposit (PaymentsService;
            // PROJECT_STATE.md Section 3ce).
            return new BookingCreateResult { Outcome = BookingCreateOutcome.Created, BookingId = booking.Id };
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

        public async Task<IEnumerable<T>> GetJobsForDayAsync<T>(DateTime day)
        {
            return await this.JobsOnDay(day)
                .OrderBy(x => x.ScheduledStart)
                .To<T>()
                .ToListAsync();
        }

        public async Task<DateTime?> GetNextDayWithJobsAsync(DateTime day)
        {
            DateTime from = day.Date.AddDays(1);

            DateTime? start = await this.JobsInTheCalendar()
                .Where(x => x.ScheduledStart >= from)
                .MinAsync(x => x.ScheduledStart);

            return start?.Date;
        }

        public async Task<DateTime?> GetPreviousDayWithJobsAsync(DateTime day)
        {
            DateTime before = day.Date;

            DateTime? start = await this.JobsInTheCalendar()
                .Where(x => x.ScheduledStart < before)
                .MaxAsync(x => x.ScheduledStart);

            return start?.Date;
        }

        public async Task<int> ReleaseAbandonedBookingsAsync(TimeSpan olderThan)
        {
            BookingStatus pendingStatus = await this.statusRepository.All().FirstOrDefaultAsync(x => x.Name == BookingStatusNames.Pending);
            BookingStatus abandonedStatus = await this.statusRepository.All().FirstOrDefaultAsync(x => x.Name == BookingStatusNames.Abandoned);
            if (pendingStatus == null || abandonedStatus == null)
            {
                return 0;
            }

            DateTime cutoff = DateTime.UtcNow - olderThan;
            List<Booking> staleBookings = await this.bookingRepository.All()
                .Where(x => x.StatusId == pendingStatus.Id && x.CreatedOn < cutoff)
                .ToListAsync();

            // Before a booking is dropped, its payment page is closed at Stripe: one left open
            // could be paid after the hour had gone back on sale. A booking whose page turns out
            // to be paid is a booking, and is left alone. One whose page could not be reached
            // keeps its hour until the next run (PROJECT_STATE.md Section 3cg).
            var droppable = new List<Booking>();
            foreach (Booking booking in staleBookings)
            {
                if (await this.paymentsService.CloseCheckoutsAsync(booking.Id) == CheckoutClosure.Closed)
                {
                    droppable.Add(booking);
                }
            }

            staleBookings = droppable;

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

        public async Task<int> GetTotalCountAsync()
        {
            return await this.bookingRepository.All().CountAsync();
        }

        public async Task<int> GetPendingCountAsync()
        {
            return await this.bookingRepository.All().CountAsync(x => x.Status.Name == BookingStatusNames.Pending);
        }

        public async Task<IReadOnlyDictionary<HeardAbout, int>> GetHeardAboutCountsAsync(DateTime? since)
        {
            IQueryable<Booking> jobs = this.bookingRepository.All();
            if (since.HasValue)
            {
                jobs = jobs.Where(x => x.CreatedOn >= since.Value);
            }

            return await jobs
                .GroupBy(x => x.HeardAbout)
                .Select(x => new { Answer = x.Key, Count = x.Count() })
                .ToDictionaryAsync(x => x.Answer, x => x.Count);
        }

        public BookingSummaryStats GetSummaryStats(IEnumerable<BookingDetailsViewModel> bookings)
        {
            // Deliberately takes the list rather than fetching it: the caller decides whether it
            // already has the right (unfiltered) list in hand or needs to fetch one, so a request
            // that isn't filtered doesn't pay for a redundant round trip.
            // A cancelled or abandoned job keeps its date now, so it has to be left out by name:
            // while it lost its date with its slot it fell out of both figures by accident.
            // A website booking still waiting for its deposit is left out too: it is not a job
            // until the deposit is in, and most of those are gone a quarter of an hour later.
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

        // Every job that is on or done, written-in ones too. A cancelled or abandoned job is left
        // out: the admin opens a day to see who is going where. A day's list and the arrows that
        // lead to the nearest day with jobs all read this, so a lit arrow never opens an empty list.
        private IQueryable<Booking> JobsInTheCalendar()
        {
            return this.bookingRepository.All()
                .Where(x => x.Status.Name != BookingStatusNames.Cancelled && x.Status.Name != BookingStatusNames.Abandoned);
        }

        private IQueryable<Booking> JobsOnDay(DateTime day)
        {
            DateTime from = day.Date;
            DateTime to = from.AddDays(1);

            return this.JobsInTheCalendar()
                .Where(x => x.ScheduledStart >= from && x.ScheduledStart < to);
        }
    }
}
