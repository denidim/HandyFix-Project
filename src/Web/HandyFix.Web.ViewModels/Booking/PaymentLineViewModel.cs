namespace HandyFix.Web.ViewModels.Booking
{
    using System;
    using System.Globalization;

    using HandyFix.Common;
    using HandyFix.Data.Models;
    using HandyFix.Services.Mapping;

    using Mapster;

    using IMapFromPayment = HandyFix.Services.Mapping.IMapFrom<HandyFix.Data.Models.Payment>;

    /// <summary>
    /// One line of a job's money list: the deposit paid on the website, or a payment the admin
    /// wrote on the job.
    /// </summary>
    public class PaymentLineViewModel : IMapFromPayment, IHaveCustomMappings
    {
        public Guid Id { get; set; }

        public decimal Amount { get; set; }

        public string Method { get; set; }

        public string StatusName { get; set; }

        public DateTime CreatedOn { get; set; }

        /// <summary>
        /// A tip the customer added when paying the invoice online, apart from the amount.
        /// </summary>
        public decimal? Tip { get; set; }

        /// <summary>
        /// An invoice the customer paid on its payment page: written by the site, not the admin.
        /// </summary>
        public bool IsPaidOnline { get; set; }

        /// <summary>
        /// The deposit paid on the website, whether it stayed or was sent back. Anything else
        /// was typed in by the admin and can be taken off again.
        /// </summary>
        public bool IsWebsiteDeposit => this.StatusName == PaymentStatusNames.DepositPaid || this.StatusName == PaymentStatusNames.Refunded;

        public bool IsRefunded => this.StatusName == PaymentStatusNames.Refunded;

        public string Description => this.IsRefunded
            ? "Deposit, card on the website (refunded)"
            : this.IsWebsiteDeposit
                ? "Deposit, card on the website"
                : this.IsPaidOnline
                    ? "Card, invoice paid online" + (this.Tip > 0m ? $" (+ £{this.Tip.Value.ToString("0.00", CultureInfo.InvariantCulture)} tip)" : string.Empty)
                    : this.Method;

        public void CreateMappings(TypeAdapterConfig config)
        {
            // Only a payment page has a session id: the admin's lines never do.
            config.NewConfig<Payment, PaymentLineViewModel>()
                .Map(dest => dest.StatusName, src => src.Status.Name)
                .Map(dest => dest.IsPaidOnline, src => src.CheckoutSessionId != null);
        }
    }
}
