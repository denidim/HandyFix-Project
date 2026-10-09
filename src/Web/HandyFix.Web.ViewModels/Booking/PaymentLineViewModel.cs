namespace HandyFix.Web.ViewModels.Booking
{
    using System;

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
        /// The deposit paid on the website, whether it stayed or was sent back. Anything else
        /// was typed in by the admin and can be taken off again.
        /// </summary>
        public bool IsWebsiteDeposit => this.StatusName == "DepositPaid" || this.StatusName == "Refunded";

        public bool IsRefunded => this.StatusName == "Refunded";

        public string Description => this.IsRefunded
            ? "Deposit, card on the website (refunded)"
            : this.IsWebsiteDeposit
                ? "Deposit, card on the website"
                : this.Method;

        public void CreateMappings(TypeAdapterConfig config)
        {
            config.NewConfig<Payment, PaymentLineViewModel>()
                .Map(dest => dest.StatusName, src => src.Status.Name);
        }
    }
}
