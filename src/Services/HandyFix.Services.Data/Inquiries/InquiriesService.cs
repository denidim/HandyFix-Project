namespace HandyFix.Services.Data.Inquiries
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Common;
    using HandyFix.Data.Common.Repositories;
    using HandyFix.Data.Models;
    using HandyFix.Services;
    using HandyFix.Services.Data.Common;
    using HandyFix.Services.Mapping;
    using HandyFix.Services.Messaging;
    using HandyFix.Web.ViewModels.Administration.Enquiries;
    using HandyFix.Web.ViewModels.Home;

    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;

    public class InquiriesService : IInquiriesService
    {
        private static readonly TimeSpan DuplicateWindow = TimeSpan.FromMinutes(10);

        private readonly IDeletableEntityRepository<Inquiry> inquiryRepository;
        private readonly IDeletableEntityRepository<InquiryImage> imageRepository;
        private readonly IImageService imageService;
        private readonly IEmailSender emailSender;
        private readonly IConfiguration configuration;
        private readonly ILogger<InquiriesService> logger;

        public InquiriesService(
            IDeletableEntityRepository<Inquiry> inquiryRepository,
            IDeletableEntityRepository<InquiryImage> imageRepository,
            IImageService imageService,
            IEmailSender emailSender,
            IConfiguration configuration,
            ILogger<InquiriesService> logger)
        {
            this.inquiryRepository = inquiryRepository;
            this.imageRepository = imageRepository;
            this.imageService = imageService;
            this.emailSender = emailSender;
            this.configuration = configuration;
            this.logger = logger;
        }

        // The same person sending the same words again within a few minutes is one enquiry sent
        // twice: a double click, a Back and resend, a page reloaded on the "send again?" prompt.
        // It is thanked like the first and not saved again. A second enquiry that says anything
        // different is a new enquiry, however soon it comes.
        public async Task<bool> IsRecentDuplicateAsync(ContactInputModel model)
        {
            DateTime since = DateTime.UtcNow - DuplicateWindow;
            var email = (model.Email ?? string.Empty).Trim().ToLower();
            var message = BuildMessage(model);

            return await this.inquiryRepository.All()
                .AnyAsync(x => x.CreatedOn >= since && x.Email.ToLower() == email && x.Message == message);
        }

        public async Task CreateInquiryAsync(ContactInputModel model, IReadOnlyList<string> imageUrls)
        {
            var inquiry = new Inquiry
            {
                Name = model.Name,
                Email = model.Email,
                PhoneNumber = model.PhoneNumber,
                Message = BuildMessage(model),
            };

            await this.inquiryRepository.AddAsync(inquiry);
            await this.inquiryRepository.SaveChangesAsync();

            if (imageUrls != null && imageUrls.Count > 0)
            {
                foreach (var url in imageUrls)
                {
                    var img = new InquiryImage
                    {
                        InquiryId = inquiry.Id,
                        ImageUrl = url,
                    };
                    await this.imageRepository.AddAsync(img);
                }

                await this.imageRepository.SaveChangesAsync();
            }

            // After the save, never before it: the enquiry is what matters, and it is in the
            // admin list whether or not an email gets out.
            await this.NotifyTheCompanyAsync(inquiry, imageUrls, model.PhotosNotSaved);
            await this.AcknowledgeToTheSenderAsync(inquiry);
        }

        public async Task<IEnumerable<T>> GetAllAsync<T>(
            InquirySortField sortField = InquirySortField.CreatedOn,
            bool descending = true)
        {
            IQueryable<Inquiry> query = this.inquiryRepository.All();

            query = sortField switch
            {
                InquirySortField.Name => descending
                    ? query.OrderByDescending(x => x.Name)
                    : query.OrderBy(x => x.Name),
                _ => descending
                    ? query.OrderByDescending(x => x.CreatedOn)
                    : query.OrderBy(x => x.CreatedOn),
            };

            return await query.To<T>().ToListAsync();
        }

        public async Task<T> GetByIdAsync<T>(Guid id)
        {
            return await this.inquiryRepository.All()
                .Where(x => x.Id == id)
                .To<T>()
                .FirstOrDefaultAsync();
        }

        // A real delete, not the soft one the repository does by default: an enquiry is a person's
        // name, email, phone number and message, and "Delete Permanent" has to mean it for a
        // request to erase them to be met from the admin panel (PROJECT_STATE Section 3cb).
        public async Task DeleteAsync(Guid id)
        {
            Inquiry inquiry = await this.inquiryRepository.All()
                .FirstOrDefaultAsync(x => x.Id == id);

            if (inquiry == null)
            {
                return;
            }

            List<InquiryImage> images = await this.imageRepository.AllWithDeleted()
                .Where(x => x.InquiryId == id)
                .ToListAsync();

            // The photos leave storage first. If that fails, it throws and the rows stay, so the
            // admin can try again; the other order could leave photos that nothing points at.
            await this.imageService.DeleteImagesAsync(images.Select(x => x.ImageUrl));

            foreach (InquiryImage image in images)
            {
                this.imageRepository.HardDelete(image);
            }

            this.inquiryRepository.HardDelete(inquiry);

            // Both repositories share one context, so this one save removes the photo rows and
            // the enquiry together.
            await this.inquiryRepository.SaveChangesAsync();
        }

        public async Task<int> GetTotalCountAsync()
        {
            return await this.inquiryRepository.All().CountAsync();
        }

        // The Contact form sends its category as a separate field, validated separately from the
        // visitor's own text; it's stored as a prefix so the admin Enquiries list reads the same
        // as it always has. Join Our Team sends no category.
        private static string BuildMessage(ContactInputModel model)
        {
            return string.IsNullOrWhiteSpace(model.Category)
                ? model.Message
                : $"[Category: {model.Category.Trim()}] {model.Message}";
        }

        // A job application is saved as an enquiry whose message starts with a marker.
        private static bool IsJobApplication(Inquiry inquiry)
        {
            return inquiry.Message.StartsWith(JoinTeamInputModel.MessagePrefix, StringComparison.Ordinal);
        }

        // To the company's one inbox, so an enquiry is seen without anyone opening the admin
        // panel. Its Reply-To is the sender's address: pressing Reply answers them, not the site.
        // Everything the sender typed is encoded before it goes into the HTML.
        private async Task NotifyTheCompanyAsync(Inquiry inquiry, IReadOnlyList<string> imageUrls, int photosNotSaved)
        {
            var what = IsJobApplication(inquiry) ? "job application" : "enquiry";

            var photos = string.Empty;
            if (imageUrls != null && imageUrls.Count > 0)
            {
                // A photo's address ends in the name its file had, which can hold spaces.
                IEnumerable<string> links = imageUrls.Select((url, i) => $@"<a href=""{EmailText.Encode(url).Replace(" ", "%20")}"">Photo {i + 1}</a>");
                photos = $"<p><strong>Photos:</strong> {string.Join(", ", links)}</p>";
            }

            if (photosNotSaved > 0)
            {
                photos += $"<p><strong>Photos attached but not saved: {photosNotSaved}.</strong> Storage could not be reached when this was sent. Please ask for them again if you need them.</p>";
            }

            var body = $@"
                <h3>A new {what} has come in from the website.</h3>
                <ul>
                    <li><strong>Name:</strong> {EmailText.Encode(inquiry.Name)}</li>
                    <li><strong>Email:</strong> {EmailText.Encode(inquiry.Email)}</li>
                    <li><strong>Phone:</strong> {EmailText.Encode(inquiry.PhoneNumber)}</li>
                </ul>
                <p>{EmailText.EncodeLines(inquiry.Message)}</p>
                {photos}
                <p>Reply to this email to answer them directly. It is also in the admin panel, under Enquiries.</p>";

            await this.emailSender.TrySendEmailAsync(
                this.logger,
                $"{what} notice to the company",
                EmailSettings.SystemFromAddress(this.configuration),
                EmailSettings.WebsiteFromName,
                EmailSettings.AdminNotificationAddress(this.configuration),
                $"New {what} - {inquiry.Name}",
                body,
                replyTo: inquiry.Email);
        }

        // To the address the sender gave, so they know it arrived. It deliberately repeats
        // nothing they typed, not even their name: anyone can put any address into a public form,
        // and an email that echoed the form back would let the site be used to send a stranger
        // whatever text a bot chose, from the business's own domain.
        private async Task AcknowledgeToTheSenderAsync(Inquiry inquiry)
        {
            var isApplication = IsJobApplication(inquiry);
            var what = isApplication ? "application" : "enquiry";
            var ifUrgent = isApplication
                ? string.Empty
                : $"<p>If it is urgent, call us or message us on WhatsApp on {GlobalConstants.BusinessPhone}.</p>";

            var body = $@"
                <h3>Hello,</h3>
                <p>Thank you for getting in touch with <strong>{GlobalConstants.SystemName}</strong>. We have received your {what} and will be in touch soon.</p>
                {ifUrgent}
                <p>If you did not send us anything, you can ignore this email.</p>
                <p>Best regards,<br /><strong>The {GlobalConstants.SystemName} Team</strong></p>";

            await this.emailSender.TrySendEmailAsync(
                this.logger,
                $"{what} acknowledgement to the sender",
                EmailSettings.BookingsFromAddress(this.configuration),
                EmailSettings.CustomerFromName,
                inquiry.Email,
                $"We have received your {what}",
                body);
        }
    }
}
