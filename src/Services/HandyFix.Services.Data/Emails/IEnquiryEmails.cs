namespace HandyFix.Services.Data.Emails
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using HandyFix.Data.Models;

    // The two emails sent when the Contact or Join Our Team form is sent. Each answers whether
    // the email went, and neither throws; what is kept with the enquiry when one did not go is
    // decided by InquiriesService.
    public interface IEnquiryEmails
    {
        // To the company, with everything the sender typed and links to their photos.
        Task<bool> NoticeToTheCompanyAsync(Inquiry inquiry, IReadOnlyList<string> imageUrls, int photosNotSaved);

        // To the sender, so they know it arrived.
        Task<bool> AcknowledgementToTheSenderAsync(Inquiry inquiry);
    }
}
