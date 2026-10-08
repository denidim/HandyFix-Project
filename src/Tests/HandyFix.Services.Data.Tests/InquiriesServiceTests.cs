namespace HandyFix.Services.Data.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Data;
    using HandyFix.Data.Models;
    using HandyFix.Data.Repositories;
    using HandyFix.Services;
    using HandyFix.Services.Data.Inquiries;
    using HandyFix.Services.Messaging;
    using HandyFix.Web.ViewModels.Administration.Enquiries;
    using HandyFix.Web.ViewModels.Home;

    using Microsoft.Data.Sqlite;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging.Abstractions;

    using Moq;

    using Xunit;

    public class InquiriesServiceTests
    {
        [Fact]
        public async Task CreateInquiryAsyncShouldSaveToDatabaseWithImages()
        {
            using ApplicationDbContext dbContext = InMemoryContext();
            InquiriesService service = BuildService(dbContext);

            var imageUrls = new List<string> { "/uploads/inquiries/some_image_file.jpg" };
            var model = new ContactInputModel
            {
                Name = "Jane Doe",
                Email = "jane@example.com",
                PhoneNumber = "07123456789",
                Message = "Needs leak repairs urgently.",
            };

            await service.CreateInquiryAsync(model, imageUrls);

            Assert.Equal(1, dbContext.Inquiries.Count());
            var inquiry = dbContext.Inquiries.Include(x => x.Images).First();
            Assert.Equal("Jane Doe", inquiry.Name);
            Assert.Equal("jane@example.com", inquiry.Email);
            Assert.Equal("07123456789", inquiry.PhoneNumber);
            Assert.Equal("Needs leak repairs urgently.", inquiry.Message);

            Assert.Single(inquiry.Images);
            Assert.Equal("/uploads/inquiries/some_image_file.jpg", inquiry.Images.First().ImageUrl);
        }

        // Guards PROJECT_STATE.md Section 3ax: the Contact page used to write the category into the
        // message in the browser before validation ran, so an empty message passed. The category is
        // now its own field and the prefix is added here, after the visitor's own text is validated.
        [Fact]
        public async Task CreateInquiryAsyncShouldPrefixMessageWithCategoryWhenProvided()
        {
            using ApplicationDbContext dbContext = InMemoryContext();
            InquiriesService service = BuildService(dbContext);

            await service.CreateInquiryAsync(
                new ContactInputModel
                {
                    Name = "Jane Doe",
                    Email = "jane@example.com",
                    PhoneNumber = "07123456789",
                    Message = "Kitchen tap is dripping.",
                    Category = "Plumbing",
                },
                new List<string>());

            Assert.Equal("[Category: Plumbing] Kitchen tap is dripping.", dbContext.Inquiries.Single().Message);
        }

        [Fact]
        public async Task GetAllAsyncShouldDefaultToCreatedOnDescending()
        {
            using ApplicationDbContext dbContext = InMemoryContext();

            var older = new Inquiry { Name = "Alice Older", Email = "alice@example.com", PhoneNumber = "07000000001", Message = "First message here." };
            var newer = new Inquiry { Name = "Bob Newer", Email = "bob@example.com", PhoneNumber = "07000000002", Message = "Second message here." };
            dbContext.Inquiries.Add(older);
            dbContext.Inquiries.Add(newer);
            await dbContext.SaveChangesAsync();

            // Back-date CreatedOn explicitly rather than relying on incidental
            // real-time gaps between saves.
            older.CreatedOn = DateTime.UtcNow.AddDays(-1);
            await dbContext.SaveChangesAsync();

            InquiriesService service = BuildService(dbContext);
            var results = (await service.GetAllAsync<EnquiryViewModel>()).ToList();

            Assert.Equal(newer.Id, results.First().Id);
            Assert.Equal(older.Id, results.Last().Id);
        }

        [Fact]
        public async Task GetAllAsyncShouldSortByNameAscendingWhenRequested()
        {
            using ApplicationDbContext dbContext = InMemoryContext();

            dbContext.Inquiries.Add(new Inquiry { Name = "Zack", Email = "zack@example.com", PhoneNumber = "07000000003", Message = "Needs a plumber urgently." });
            dbContext.Inquiries.Add(new Inquiry { Name = "Amy", Email = "amy@example.com", PhoneNumber = "07000000004", Message = "Needs a handyman urgently." });
            await dbContext.SaveChangesAsync();

            InquiriesService service = BuildService(dbContext);
            var results = (await service.GetAllAsync<EnquiryViewModel>(InquirySortField.Name, descending: false)).ToList();

            Assert.Equal("Amy", results.First().Name);
            Assert.Equal("Zack", results.Last().Name);
        }

        // A double click on Send, or a resend after Back, used to save the enquiry twice. The same
        // email with the same words inside ten minutes is the first enquiry again; anything
        // different is a new one, however soon it comes (PROJECT_STATE.md Section 3cb). On Sqlite,
        // so the comparison is the one a real database makes.
        [Fact]
        public async Task IsRecentDuplicateAsyncShouldKnowTheSameEnquirySentAgain()
        {
            using var connection = new SqliteConnection("DataSource=:memory:");
            using ApplicationDbContext dbContext = SqliteContext(connection);
            InquiriesService service = BuildService(dbContext);

            ContactInputModel Enquiry(string email = "jane@example.com", string message = "Kitchen tap is dripping.", string category = "Plumbing") =>
                new ContactInputModel { Name = "Jane Doe", Email = email, PhoneNumber = "07123456789", Message = message, Category = category };

            Assert.False(await service.IsRecentDuplicateAsync(Enquiry()));

            await service.CreateInquiryAsync(Enquiry(), new List<string>());

            Assert.True(await service.IsRecentDuplicateAsync(Enquiry()));
            Assert.True(await service.IsRecentDuplicateAsync(Enquiry(email: " Jane@Example.COM ")));

            Assert.False(await service.IsRecentDuplicateAsync(Enquiry(message: "Kitchen tap is dripping. It is the cold one.")));
            Assert.False(await service.IsRecentDuplicateAsync(Enquiry(email: "john@example.com")));
            Assert.False(await service.IsRecentDuplicateAsync(Enquiry(category: "Handyman")));
        }

        [Fact]
        public async Task IsRecentDuplicateAsyncShouldTreatTheSameWordsLaterAsANewEnquiry()
        {
            using var connection = new SqliteConnection("DataSource=:memory:");
            using ApplicationDbContext dbContext = SqliteContext(connection);
            InquiriesService service = BuildService(dbContext);
            var model = new ContactInputModel { Name = "Jane Doe", Email = "jane@example.com", PhoneNumber = "07123456789", Message = "Kitchen tap is dripping." };

            await service.CreateInquiryAsync(model, new List<string>());
            dbContext.Inquiries.Single().CreatedOn = DateTime.UtcNow.AddMinutes(-11);
            await dbContext.SaveChangesAsync();

            Assert.False(await service.IsRecentDuplicateAsync(model));
        }

        // A deleted enquiry is gone: sending the same words again after it was deleted is new.
        [Fact]
        public async Task IsRecentDuplicateAsyncShouldNotCountADeletedEnquiry()
        {
            using var connection = new SqliteConnection("DataSource=:memory:");
            using ApplicationDbContext dbContext = SqliteContext(connection);
            InquiriesService service = BuildService(dbContext);
            var model = new ContactInputModel { Name = "Jane Doe", Email = "jane@example.com", PhoneNumber = "07123456789", Message = "Kitchen tap is dripping." };

            await service.CreateInquiryAsync(model, new List<string>());
            await service.DeleteAsync(dbContext.Inquiries.Single().Id);

            Assert.False(await service.IsRecentDuplicateAsync(model));
        }

        // An enquiry used to sit in the admin list until someone opened it, and the sender heard
        // nothing. Now the company's inbox gets a notice and the sender an acknowledgement
        // (PROJECT_STATE.md Section 3cb).
        [Fact]
        public async Task CreateInquiryAsyncShouldTellTheCompanyAndAcknowledgeToTheSender()
        {
            using ApplicationDbContext dbContext = InMemoryContext();
            var emailSender = new Mock<IEmailSender>();
            List<SentEmail> sent = CaptureEmails(emailSender);

            await BuildService(dbContext, emailSender: emailSender).CreateInquiryAsync(
                new ContactInputModel
                {
                    Name = "Jane Doe",
                    Email = "jane@example.com",
                    PhoneNumber = "07700 900123",
                    Message = "Kitchen tap is dripping.\nIt is the cold one.",
                    Category = "Plumbing",
                },
                new List<string> { "https://photos.example/inquiries/abc_my tap.jpg" });

            Assert.Equal(2, sent.Count);

            SentEmail notice = sent[0];
            Assert.Equal("info@plumbing-handyman-surrey.co.uk", notice.To);
            Assert.Equal("New enquiry - Jane Doe", notice.Subject);

            // Pressing Reply answers the sender, not the website.
            Assert.Equal("jane@example.com", notice.ReplyTo);
            Assert.Contains("Jane Doe", notice.Body);
            Assert.Contains("07700 900123", notice.Body);
            Assert.Contains("[Category: Plumbing] Kitchen tap is dripping.<br />It is the cold one.", notice.Body);
            Assert.Contains("<a href=\"https://photos.example/inquiries/abc_my%20tap.jpg\">Photo 1</a>", notice.Body);

            SentEmail acknowledgement = sent[1];
            Assert.Equal("jane@example.com", acknowledgement.To);
            Assert.Equal("bookings@plumbing-handyman-surrey.co.uk", acknowledgement.From);
            Assert.Equal("We have received your enquiry", acknowledgement.Subject);
            Assert.Null(acknowledgement.ReplyTo);
            Assert.Contains("020 3951 5915", acknowledgement.Body);
        }

        // Anyone can type any address into a public form. An acknowledgement that repeated the
        // form would let the site send a stranger whatever a bot wrote, from the business's own
        // domain, so it repeats nothing: not the message, not the phone, not even the name.
        [Fact]
        public async Task TheAcknowledgementShouldRepeatNothingTheSenderTyped()
        {
            using ApplicationDbContext dbContext = InMemoryContext();
            var emailSender = new Mock<IEmailSender>();
            List<SentEmail> sent = CaptureEmails(emailSender);

            await BuildService(dbContext, emailSender: emailSender).CreateInquiryAsync(
                new ContactInputModel
                {
                    Name = "Winnie Prizewinner",
                    Email = "stranger@example.com",
                    PhoneNumber = "07700 900123",
                    Message = "You have won. Claim at spam dot example today.",
                    Category = "Other",
                },
                new List<string>());

            SentEmail acknowledgement = sent.Single(e => e.To == "stranger@example.com");
            Assert.DoesNotContain("Winnie", acknowledgement.Body);
            Assert.DoesNotContain("Prizewinner", acknowledgement.Subject);
            Assert.DoesNotContain("spam dot example", acknowledgement.Body);
            Assert.DoesNotContain("07700", acknowledgement.Body);
            Assert.Contains("If you did not send us anything", acknowledgement.Body);
        }

        // The emails are HTML built as text. A message written into one as typed could carry a
        // link, an image or a whole fake paragraph into the company's inbox.
        [Fact]
        public async Task TheNoticeToTheCompanyShouldCarryWhatWasTypedAsTextNotAsHtml()
        {
            using ApplicationDbContext dbContext = InMemoryContext();
            var emailSender = new Mock<IEmailSender>();
            List<SentEmail> sent = CaptureEmails(emailSender);

            await BuildService(dbContext, emailSender: emailSender).CreateInquiryAsync(
                new ContactInputModel
                {
                    Name = "Jane Doe",
                    Email = "jane@example.com",
                    PhoneNumber = "07700 900123",
                    Message = "<img src=x onerror=alert(1)> <a href=\"https://evil.example\">Pay your invoice here</a>",
                },
                new List<string> { "https://photos.example/inquiries/abc_\"><script>alert(1)</script>.jpg" });

            SentEmail notice = sent[0];
            Assert.DoesNotContain("<img", notice.Body);
            Assert.DoesNotContain("<script", notice.Body);
            Assert.DoesNotContain("href=\"https://evil.example\"", notice.Body);
            Assert.Contains("&lt;img src=x onerror=alert(1)&gt;", notice.Body);
            Assert.Contains("Pay your invoice here", notice.Body);
        }

        [Fact]
        public async Task AJobApplicationShouldBeAnnouncedAndAcknowledgedAsOne()
        {
            using ApplicationDbContext dbContext = InMemoryContext();
            var emailSender = new Mock<IEmailSender>();
            List<SentEmail> sent = CaptureEmails(emailSender);

            var application = new JoinTeamInputModel
            {
                Name = "Sam Fitter",
                Email = "sam@example.com",
                PhoneNumber = "07700 900456",
                Trade = "Plumbing",
                YearsExperience = 12,
                Availability = "Full-time",
            };

            await BuildService(dbContext, emailSender: emailSender).CreateInquiryAsync(application.ToContactInputModel(), new List<string>());

            Assert.Equal("New job application - Sam Fitter", sent[0].Subject);
            Assert.Contains("Trade: Plumbing", sent[0].Body);
            Assert.Equal("We have received your application", sent[1].Subject);

            // "If it is urgent, call us" is for a customer with a leak, not for an applicant.
            Assert.DoesNotContain("urgent", sent[1].Body);
        }

        // The enquiry is saved before either email is tried. A send that fails is logged and the
        // other still goes; the visitor is not shown an error for an enquiry that was received.
        [Fact]
        public async Task CreateInquiryAsyncShouldKeepTheEnquiryWhenAnEmailCannotBeSent()
        {
            using ApplicationDbContext dbContext = InMemoryContext();
            var emailSender = new Mock<IEmailSender>();
            emailSender
                .Setup(x => x.SendEmailAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    "info@plumbing-handyman-surrey.co.uk",
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<IEnumerable<EmailAttachment>>(),
                    It.IsAny<string>()))
                .ThrowsAsync(new InvalidOperationException("Brevo email send failed (500)"));

            await BuildService(dbContext, emailSender: emailSender).CreateInquiryAsync(
                new ContactInputModel { Name = "Jane Doe", Email = "jane@example.com", PhoneNumber = "07700 900123", Message = "Kitchen tap is dripping." },
                new List<string>());

            Assert.Single(dbContext.Inquiries);
            emailSender.Verify(
                x => x.SendEmailAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    "jane@example.com",
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<IEnumerable<EmailAttachment>>(),
                    It.IsAny<string>()),
                Times.Once);
        }

        // Storage out of reach no longer loses the enquiry (HomeController saves it without its
        // photos). The notice says photos were attached and lost, so someone can ask again.
        [Fact]
        public async Task TheNoticeShouldSayWhenAttachedPhotosCouldNotBeSaved()
        {
            using ApplicationDbContext dbContext = InMemoryContext();
            var emailSender = new Mock<IEmailSender>();
            List<SentEmail> sent = CaptureEmails(emailSender);

            await BuildService(dbContext, emailSender: emailSender).CreateInquiryAsync(
                new ContactInputModel { Name = "Jane Doe", Email = "jane@example.com", PhoneNumber = "07700 900123", Message = "Kitchen tap is dripping.", PhotosNotSaved = 3 },
                new List<string>());

            Assert.Contains("Photos attached but not saved: 3.", sent[0].Body);
            Assert.DoesNotContain("not saved", sent[1].Body);
        }

        // Staging sends from, and to, addresses verified in its own Brevo account.
        [Fact]
        public async Task TheEnquiryEmailsShouldUseTheConfiguredAddresses()
        {
            using ApplicationDbContext dbContext = InMemoryContext();
            var emailSender = new Mock<IEmailSender>();
            List<SentEmail> sent = CaptureEmails(emailSender);
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["Admin:NotificationEmail"] = "inbox@staging.example",
                    ["Email:SystemFromAddress"] = "system@staging.example",
                    ["Email:BookingsFromAddress"] = "hello@staging.example",
                })
                .Build();

            await BuildService(dbContext, emailSender: emailSender, configuration: configuration).CreateInquiryAsync(
                new ContactInputModel { Name = "Jane Doe", Email = "jane@example.com", PhoneNumber = "07700 900123", Message = "Kitchen tap is dripping." },
                new List<string>());

            Assert.Equal("inbox@staging.example", sent[0].To);
            Assert.Equal("system@staging.example", sent[0].From);
            Assert.Equal("hello@staging.example", sent[1].From);
        }

        // "Delete Permanent" used to set IsDeleted and keep the row: the name, email, phone number
        // and message stayed in the database, hidden from every page, and the photos stayed in
        // storage (PROJECT_STATE.md Section 3cb). On Sqlite, because the photo rows point at the
        // enquiry with a real foreign key and InMemory would not notice them left behind.
        [Fact]
        public async Task DeleteAsyncShouldRemoveTheEnquiryItsPhotoRowsAndItsPhotosForGood()
        {
            using var connection = new SqliteConnection("DataSource=:memory:");
            using ApplicationDbContext dbContext = SqliteContext(connection);
            Inquiry inquiry = await AddInquiryWithPhotosAsync(dbContext);
            Inquiry other = await AddInquiryWithPhotosAsync(dbContext, "https://photos.example/inquiries/other.jpg");

            var imageService = new Mock<IImageService>();
            IEnumerable<string> removedFromStorage = null;
            imageService
                .Setup(x => x.DeleteImagesAsync(It.IsAny<IEnumerable<string>>()))
                .Callback<IEnumerable<string>>(urls => removedFromStorage = urls.ToList())
                .Returns(Task.CompletedTask);

            await BuildService(dbContext, imageService).DeleteAsync(inquiry.Id);

            // IgnoreQueryFilters, because a soft delete would also pass a filtered check.
            Assert.Empty(dbContext.Inquiries.IgnoreQueryFilters().Where(x => x.Id == inquiry.Id));
            Assert.Empty(dbContext.InquiryImages.IgnoreQueryFilters().Where(x => x.InquiryId == inquiry.Id));
            Assert.Equal(
                new[] { "https://photos.example/inquiries/a.jpg", "https://photos.example/inquiries/b.jpg" },
                removedFromStorage.OrderBy(url => url));

            // Another enquiry and its photo are not touched.
            Assert.Single(dbContext.Inquiries.Where(x => x.Id == other.Id));
            Assert.Single(dbContext.InquiryImages.Where(x => x.InquiryId == other.Id));
        }

        [Fact]
        public async Task DeleteAsyncShouldKeepEverythingWhenThePhotosCannotBeRemovedFromStorage()
        {
            using var connection = new SqliteConnection("DataSource=:memory:");
            using ApplicationDbContext dbContext = SqliteContext(connection);
            Inquiry inquiry = await AddInquiryWithPhotosAsync(dbContext);

            var imageService = new Mock<IImageService>();
            imageService
                .Setup(x => x.DeleteImagesAsync(It.IsAny<IEnumerable<string>>()))
                .ThrowsAsync(new InvalidOperationException("Storage is unreachable."));

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => BuildService(dbContext, imageService).DeleteAsync(inquiry.Id));

            // The rows are what the admin's second try works from.
            Assert.Single(dbContext.Inquiries.Where(x => x.Id == inquiry.Id));
            Assert.Equal(2, dbContext.InquiryImages.Count(x => x.InquiryId == inquiry.Id));
        }

        [Fact]
        public async Task DeleteAsyncShouldDoNothingForAnUnknownEnquiry()
        {
            using var connection = new SqliteConnection("DataSource=:memory:");
            using ApplicationDbContext dbContext = SqliteContext(connection);
            await AddInquiryWithPhotosAsync(dbContext);

            var imageService = new Mock<IImageService>();

            await BuildService(dbContext, imageService).DeleteAsync(Guid.NewGuid());

            Assert.Single(dbContext.Inquiries);
            imageService.Verify(x => x.DeleteImagesAsync(It.IsAny<IEnumerable<string>>()), Times.Never);
        }

        private static InquiriesService BuildService(
            ApplicationDbContext dbContext,
            Mock<IImageService> imageService = null,
            Mock<IEmailSender> emailSender = null,
            IConfiguration configuration = null)
        {
            return new InquiriesService(
                new EfDeletableEntityRepository<Inquiry>(dbContext),
                new EfDeletableEntityRepository<InquiryImage>(dbContext),
                (imageService ?? new Mock<IImageService>()).Object,
                (emailSender ?? new Mock<IEmailSender>()).Object,
                configuration ?? new ConfigurationBuilder().Build(),
                NullLogger<InquiriesService>.Instance);
        }

        // Every email the service sent, in order, as (to, subject, body, replyTo, from, fromName).
        private static List<SentEmail> CaptureEmails(Mock<IEmailSender> emailSender)
        {
            var sent = new List<SentEmail>();
            emailSender
                .Setup(x => x.SendEmailAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<IEnumerable<EmailAttachment>>(),
                    It.IsAny<string>()))
                .Callback<string, string, string, string, string, IEnumerable<EmailAttachment>, string>(
                    (from, fromName, to, subject, body, attachments, replyTo) =>
                        sent.Add(new SentEmail { From = from, FromName = fromName, To = to, Subject = subject, Body = body, ReplyTo = replyTo }))
                .Returns(Task.CompletedTask);
            return sent;
        }

        private static ApplicationDbContext InMemoryContext()
        {
            return new ApplicationDbContext(
                new DbContextOptionsBuilder<ApplicationDbContext>()
                    .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options);
        }

        private static ApplicationDbContext SqliteContext(SqliteConnection connection)
        {
            connection.Open();

            var dbContext = new ApplicationDbContext(
                new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
            dbContext.Database.EnsureCreated();
            return dbContext;
        }

        private static async Task<Inquiry> AddInquiryWithPhotosAsync(ApplicationDbContext dbContext, params string[] photoUrls)
        {
            if (photoUrls.Length == 0)
            {
                photoUrls = new[] { "https://photos.example/inquiries/a.jpg", "https://photos.example/inquiries/b.jpg" };
            }

            var inquiry = new Inquiry
            {
                Name = "Jane Doe",
                Email = "jane@example.com",
                PhoneNumber = "07123456789",
                Message = "Kitchen tap is dripping.",
            };

            foreach (var url in photoUrls)
            {
                inquiry.Images.Add(new InquiryImage { ImageUrl = url });
            }

            dbContext.Inquiries.Add(inquiry);
            await dbContext.SaveChangesAsync();
            return inquiry;
        }

        private sealed class SentEmail
        {
            public string From { get; set; }

            public string FromName { get; set; }

            public string To { get; set; }

            public string Subject { get; set; }

            public string Body { get; set; }

            public string ReplyTo { get; set; }
        }
    }
}
