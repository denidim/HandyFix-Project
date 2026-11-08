namespace HandyFix.Services.Data.Invoices
{
    using System;

    using HandyFix.Common;

    using Microsoft.Extensions.Configuration;

    // How a customer pays what is left, as the invoice tells them. The company's bank details
    // come from the server's configuration, never from the code: this repository is public, and
    // bank details printed in it are an invitation to fake invoices. Until they are set the
    // invoice gives the phone number, which is true either way.
    public class InvoiceOptions
    {
        public const string DefaultPaymentDetails = "To pay the balance, call us on " + GlobalConstants.BusinessPhone + " or reply to the email this invoice came with.";

        public string PaymentDetails { get; set; } = DefaultPaymentDetails;

        public string SiteUrl { get; set; } = GlobalConstants.SiteUrl;

        public string PaymentUrl(Guid bookingId) => $"{this.SiteUrl}/Payment/Invoice/{bookingId}";

        public void ReadFrom(IConfiguration configuration)
        {
            var value = configuration["Invoice:PaymentDetails"];
            this.PaymentDetails = string.IsNullOrWhiteSpace(value) ? DefaultPaymentDetails : value.Trim();

            var siteUrl = configuration["Site:PublicUrl"];
            this.SiteUrl = string.IsNullOrWhiteSpace(siteUrl) ? GlobalConstants.SiteUrl : siteUrl.Trim().TrimEnd('/');
        }
    }
}
