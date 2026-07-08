namespace HandyFix.Services.Data.Bookings
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Data.Common.Repositories;
    using HandyFix.Data.Models;
    using HandyFix.Services.Mapping;
    using HandyFix.Services.Messaging;

    using Mapster;

    using Microsoft.EntityFrameworkCore;

    public class BookingsService : IBookingsService
    {
        private readonly IDeletableEntityRepository<Booking> bookingRepository;
        private readonly IDeletableEntityRepository<Service> serviceRepository;
        private readonly IDeletableEntityRepository<AvailabilitySlot> slotRepository;
        private readonly IDeletableEntityRepository<BookingStatus> statusRepository;
        private readonly IDeletableEntityRepository<BookingImage> imageRepository;
        private readonly IEmailSender emailSender;

        public BookingsService(
            IDeletableEntityRepository<Booking> bookingRepository,
            IDeletableEntityRepository<Service> serviceRepository,
            IDeletableEntityRepository<AvailabilitySlot> slotRepository,
            IDeletableEntityRepository<BookingStatus> statusRepository,
            IDeletableEntityRepository<BookingImage> imageRepository,
            IEmailSender emailSender)
        {
            this.bookingRepository = bookingRepository;
            this.serviceRepository = serviceRepository;
            this.slotRepository = slotRepository;
            this.statusRepository = statusRepository;
            this.imageRepository = imageRepository;
            this.emailSender = emailSender;
        }

        public async Task<Booking> CreateBookingAsync(
            string firstName,
            string lastName,
            string email,
            string phone,
            string address,
            string problemDescription,
            Guid slotId,
            IEnumerable<Guid> serviceIds,
            string userId = null)
        {
            var pendingStatus = await this.statusRepository.All().FirstOrDefaultAsync(x => x.Name == "Pending");
            if (pendingStatus == null)
            {
                throw new InvalidOperationException("Booking status 'Pending' is not seeded.");
            }

            var selectedServices = await this.serviceRepository.All()
                .Where(x => serviceIds.Contains(x.Id))
                .ToListAsync();

            var totalAmount = selectedServices.Sum(x => x.BasePrice);
            var depositAmount = 50.00m; // Flat booking deposit

            // 1. Build the booking object framework completely in-memory
            var booking = new Booking
            {
                CustomerFirstName = firstName,
                CustomerLastName = lastName,
                Email = email,
                PhoneNumber = phone,
                Address = address,
                ProblemDescription = problemDescription,
                StatusId = pendingStatus.Id,
                UserId = userId,
                TotalAmount = totalAmount,
                DepositAmount = depositAmount,
                BookingServices = new List<BookingService>(),
            };

            // 2. Link services directly to the navigation property list before saving
            foreach (var svc in selectedServices)
            {
                var bookingService = new BookingService
                {
                    ServiceId = svc.Id,
                    PriceAtBooking = svc.BasePrice,
                    Quantity = 1,
                };
                booking.BookingServices.Add(bookingService);
            }

            // 3. Retrieve and assign the availability slot structure
            var slot = await this.slotRepository.All().FirstOrDefaultAsync(x => x.Id == slotId);
            if (slot != null)
            {
                slot.IsBooked = true;
                booking.TechnicianId = slot.TechnicianId;

                // EF Core handles linking tracking assignments automatically 
                // through object configuration when saved.
                slot.Booking = booking;
            }

            // 4. Add to repository tracking exactly once
            await this.bookingRepository.AddAsync(booking);

            // 5. Fire a single atomic save to eliminate concurrency collisions
            await this.bookingRepository.SaveChangesAsync();

            // 6. Send booking confirmation email safely
            var subject = "Your HandyFix Booking Inquiry has been Received!";
            var body = $@"
            <h3>Hello {firstName} {lastName},</h3>
            <p>Thank you for choosing <strong>HandyFix</strong>. We have received your booking request details:</p>
            <ul>
                <li><strong>Booking Reference:</strong> {booking.Id}</li>
                <li><strong>Service(s):</strong> {string.Join(", ", selectedServices.Select(s => s.Name))}</li>
                <li><strong>Scheduled Time:</strong> {(slot != null ? slot.StartTime.ToString("dd MMM yyyy 'at' HH:mm") : string.Empty)}</li>
                <li><strong>Address:</strong> {address}</li>
            </ul>
            <p>To secure this appointment slot, please pay the deposit of £{depositAmount.ToString("F2")} on the next screen.</p>
            <p>Once paid, we will confirm your technician assignment.</p>
            <br />
            <p>Best Regards,<br/><strong>HandyFix Team</strong></p>";

            await this.emailSender.SendEmailAsync(
                "no-reply@handyfix.co.uk",
                "HandyFix Booking System",
                email,
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

        public async Task<IEnumerable<T>> GetAllBookingsAsync<T>()
        {
            return await this.bookingRepository.All()
                .OrderByDescending(x => x.CreatedOn)
                .To<T>()
                .ToListAsync();
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
            var booking = await this.bookingRepository.All().FirstOrDefaultAsync(x => x.Id == bookingId);
            var status = await this.statusRepository.All().FirstOrDefaultAsync(x => x.Name.ToLower() == statusName.ToLower());

            if (booking != null && status != null)
            {
                booking.StatusId = status.Id;
                await this.bookingRepository.SaveChangesAsync();

                if (statusName == "Approved")
                {
                    // Send Booking Confirmed email
                    var subject = "Your HandyFix Booking is CONFIRMED!";
                    var body = $@"
                        <h3>Hi {booking.CustomerFirstName},</h3>
                        <p>We are pleased to inform you that your booking reference <strong>{booking.Id}</strong> is officially confirmed.</p>
                        <p>A professional technician is scheduled for your address at the selected slot.</p>
                        <p>Thank you for choosing HandyFix!</p>";

                    await this.emailSender.SendEmailAsync(
                        "bookings@handyfix.co.uk",
                        "HandyFix Support",
                        booking.Email,
                        subject,
                        body);
                }
            }
        }

        public async Task AssignTechnicianAsync(Guid bookingId, Guid technicianId)
        {
            var booking = await this.bookingRepository.All().FirstOrDefaultAsync(x => x.Id == bookingId);
            if (booking != null)
            {
                booking.TechnicianId = technicianId;
                await this.bookingRepository.SaveChangesAsync();
            }
        }

        public async Task CancelBookingAsync(Guid bookingId)
        {
            var booking = await this.bookingRepository.All()
                .Include(x => x.AvailabilitySlot)
                .FirstOrDefaultAsync(x => x.Id == bookingId);

            var cancelledStatus = await this.statusRepository.All().FirstOrDefaultAsync(x => x.Name == "Cancelled");

            if (booking != null && cancelledStatus != null)
            {
                booking.StatusId = cancelledStatus.Id;

                // Release slot
                var slots = await this.slotRepository.All().Where(x => x.BookingId == bookingId).ToListAsync();
                foreach (var slot in slots)
                {
                    slot.IsBooked = false;
                    slot.BookingId = null;
                }

                await this.bookingRepository.SaveChangesAsync();
            }
        }

        public async Task RescheduleBookingAsync(Guid bookingId, Guid newSlotId)
        {
            var booking = await this.bookingRepository.All().FirstOrDefaultAsync(x => x.Id == bookingId);
            var newSlot = await this.slotRepository.All().FirstOrDefaultAsync(x => x.Id == newSlotId);

            if (booking != null && newSlot != null && !newSlot.IsBooked && !newSlot.IsBlocked)
            {
                // Release old slots
                var oldSlots = await this.slotRepository.All().Where(x => x.BookingId == bookingId).ToListAsync();
                foreach (var slot in oldSlots)
                {
                    slot.IsBooked = false;
                    slot.BookingId = null;
                }

                // Book new slot
                newSlot.IsBooked = true;
                newSlot.BookingId = bookingId;
                booking.TechnicianId = newSlot.TechnicianId;

                await this.bookingRepository.SaveChangesAsync();
            }
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
    }
}
