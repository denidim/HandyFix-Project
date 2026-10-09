namespace HandyFix.Services.Data.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading.Tasks;

    using HandyFix.Common;
    using HandyFix.Data.Models;
    using HandyFix.Services.Data.Payments;
    using HandyFix.Web.ViewModels.Payment;

    using Microsoft.Extensions.Configuration;

    using Moq;

    using Stripe;
    using Stripe.Checkout;

    using Xunit;

    // The deposit paid through Stripe (PROJECT_STATE.md Section 3cg). The rule behind every test
    // here is that the site believes Stripe and never the browser: a booking is confirmed when
    // Stripe says its page was paid, a page is closed at Stripe before its hour goes back on
    // sale, and money is written down wherever it lands. Stripe itself is a stand-in
    // (BookingWorld.Stripe), so each test says what Stripe answers and no test calls it.
    public class StripePaymentsTests
    {
        private const string Key = "rk_test_not_a_real_key";

        // Before, the page a customer comes back to marked the deposit paid for whatever page
        // id was in its address. The id is in the address of Stripe's own page, so anyone who
        // had opened that page could confirm their booking without paying.
        [Fact]
        public async Task AReturnFromStripeConfirmsNothingWhenStripeSaysThePageWasNotPaid()
        {
            using var world = new BookingWorld(Key);
            List<SentEmail> sent = world.CaptureEmails();
            var sessionId = await world.SeedPendingPaymentAsync("12 Main Rd, Sutton");
            Booking booking = world.DbContext.Bookings.Single();
            StripeAnswers(world, sessionId, paid: false);

            Guid? answer = await world.Payments.ConfirmCheckoutAsync(sessionId);

            // The booking is still named, so the customer can be sent on to pay for it.
            Assert.Equal(booking.Id, answer);
            Assert.Equal("Pending", world.StatusOf(booking));
            Assert.Equal(new[] { "Pending" }, world.PaymentStatusesOf(booking));
            Assert.Empty(sent);
        }

        [Fact]
        public async Task AReturnFromStripeConfirmsTheBookingWhenStripeSaysThePageWasPaid()
        {
            using var world = new BookingWorld(Key);
            List<SentEmail> sent = world.CaptureEmails();
            var sessionId = await world.SeedPendingPaymentAsync("12 Main Rd, Sutton");
            Booking booking = world.DbContext.Bookings.Single();
            StripeAnswers(world, sessionId, paid: true, paymentIntentId: "pi_real_123");

            Guid? answer = await world.Payments.ConfirmCheckoutAsync(sessionId);

            Assert.Equal(booking.Id, answer);
            Assert.Equal("Approved", world.StatusOf(booking));
            Assert.Equal(new[] { "DepositPaid" }, world.PaymentStatusesOf(booking));

            // Stripe's own id for the payment, which is what a refund is found by. It used to
            // be overwritten with one the site made up.
            Assert.Equal("pi_real_123", world.DbContext.Payments.Single().TransactionId);
            Assert.Equal(2, sent.Count);
        }

        // Stripe's message and the customer's return both arrive for one payment, in either
        // order. The emails go once and the second arrival changes nothing.
        [Fact]
        public async Task APaymentHeardOfTwiceIsWrittenDownOnce()
        {
            using var world = new BookingWorld(Key);
            List<SentEmail> sent = world.CaptureEmails();
            var sessionId = await world.SeedPendingPaymentAsync("12 Main Rd, Sutton");
            Booking booking = world.DbContext.Bookings.Single();
            StripeAnswers(world, sessionId, paid: true, paymentIntentId: "pi_real_123");

            await world.Payments.ConfirmCheckoutAsync(sessionId);
            await world.Payments.ConfirmCheckoutAsync(sessionId);

            Assert.Equal(2, sent.Count);
            Assert.Equal("pi_real_123", world.DbContext.Payments.Single().TransactionId);
            Assert.Single(world.HistoryOf(booking), line => line.StartsWith("Deposit of"));

            // Once it is paid there is nothing left to ask Stripe.
            world.Stripe.Verify(x => x.GetCheckoutAsync(sessionId), Times.Once);
        }

        [Fact]
        public async Task APageTheSiteNeverOpenedConfirmsNothingAndStripeIsNotAsked()
        {
            using var world = new BookingWorld(Key);
            await world.SeedPendingPaymentAsync("12 Main Rd, Sutton");

            Assert.Null(await world.Payments.ConfirmCheckoutAsync("cs_test_somebody_elses"));
            Assert.Null(await world.Payments.ConfirmCheckoutAsync(null));

            world.Stripe.Verify(x => x.GetCheckoutAsync(It.IsAny<string>()), Times.Never);
        }

        // The pretend payment is for a site with no Stripe key. Where a key is set, a payment
        // row left over from pretend days must not be confirmable by typing its address.
        [Fact]
        public async Task APretendPaymentIsNotConfirmedWhereARealKeyIsSet()
        {
            using var world = new BookingWorld(Key, "Development");
            var sessionId = await world.SeedPendingPaymentAsync("12 Main Rd, Sutton");
            Booking booking = world.DbContext.Bookings.Single();
            world.DbContext.Payments.Single().Provider = "Stripe-Mock";
            await world.DbContext.SaveChangesAsync();

            await world.Payments.ConfirmCheckoutAsync(sessionId);

            Assert.Equal("Pending", world.StatusOf(booking));
            world.Stripe.Verify(x => x.GetCheckoutAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task WithNoKeyInDevelopmentThePretendPaymentStandsInForStripe()
        {
            using var world = new BookingWorld(environmentName: "Development");
            Booking booking = await SeedBookingWithNoPageYetAsync(world);

            PaymentCheckoutResult result = await world.Payments.CreateCheckoutSessionAsync(booking.Id, "https://example.com/success", "https://example.com/cancel");

            Assert.True(result.IsMock);
            Assert.StartsWith("mock_session_", result.SessionId);
            Assert.Equal("Stripe-Mock", world.DbContext.Payments.Single().Provider);

            // And coming back from it confirms the booking, with Stripe never asked.
            await world.Payments.ConfirmCheckoutAsync(result.SessionId);

            Assert.Equal("Approved", world.StatusOf(booking));
            world.Stripe.VerifyNoOtherCalls();
        }

        // The pretend payment is a security control, not a convenience: outside Development a
        // missing key must fail loudly and write nothing, or the live site would report
        // bookings as paid that nobody paid for.
        [Theory]
        [InlineData("Production", null)]
        [InlineData("Staging", null)]
        [InlineData("Production", "")]
        [InlineData("Production", "   ")]
        public async Task WithNoKeyOutsideDevelopmentNoPaymentIsStartedOrFaked(string environmentName, string key)
        {
            using var world = new BookingWorld(key, environmentName);
            Booking booking = await SeedBookingWithNoPageYetAsync(world);

            InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => world.Payments.CreateCheckoutSessionAsync(booking.Id, "https://example.com/success", "https://example.com/cancel"));

            Assert.Contains("Stripe is not configured", exception.Message);
            Assert.Empty(world.DbContext.Payments);
        }

        [Fact]
        public async Task APaymentPageIsOpenedWithItsOwnEndTheCustomersEmailAndTheDeposit()
        {
            using var world = new BookingWorld(Key);
            Booking booking = await SeedBookingWithNoPageYetAsync(world);
            SessionCreateOptions asked = null;
            world.Stripe
                .Setup(x => x.CreateCheckoutAsync(It.IsAny<SessionCreateOptions>()))
                .Callback<SessionCreateOptions>(options => asked = options)
                .ReturnsAsync(new Session { Id = "cs_test_new", Url = "https://checkout.stripe.com/c/pay/cs_test_new" });

            PaymentCheckoutResult result = await world.Payments.CreateCheckoutSessionAsync(booking.Id, "https://example.com/success", "https://example.com/cancel");

            Assert.False(result.IsMock);
            Assert.Equal("https://checkout.stripe.com/c/pay/cs_test_new", result.RedirectUrl);
            Assert.Equal(5000, Assert.Single(asked.LineItems).PriceData.UnitAmount);
            Assert.Equal("ada@example.com", asked.CustomerEmail);
            Assert.Equal(booking.Id.ToString(), asked.ClientReferenceId);

            // Stripe leaves a page open for a day unless told when to end it, and takes no
            // less than half an hour.
            Assert.InRange(asked.ExpiresAt.Value, DateTime.UtcNow.AddMinutes(30), DateTime.UtcNow.AddMinutes(32));

            Payment payment = world.DbContext.Payments.Single();
            Assert.Equal("cs_test_new", payment.CheckoutSessionId);
            Assert.Equal("Stripe", payment.Provider);
            Assert.Equal(new[] { "Pending" }, world.PaymentStatusesOf(booking));
        }

        // The address that opens a payment page takes any booking's id. It used to open one
        // for a booking already paid (paid twice), for one dropped or cancelled (paid for an
        // hour no longer held) and for a job the admin wrote in, which has no deposit.
        [Theory]
        [InlineData("paid")]
        [InlineData("Abandoned")]
        [InlineData("Cancelled")]
        [InlineData("written in")]
        public async Task NoPaymentPageIsOpenedForABookingWithNothingToPay(string state)
        {
            using var world = new BookingWorld(Key);
            Booking booking;
            if (state == "written in")
            {
                booking = await world.SeedWrittenInJobAsync();
            }
            else if (state == "paid")
            {
                booking = await world.SeedPaidBookingAsync();
            }
            else
            {
                booking = await SeedBookingWithNoPageYetAsync(world);
                world.SetStatus(booking, state);
            }

            PaymentCheckoutResult result = await world.Payments.CreateCheckoutSessionAsync(booking.Id, "https://example.com/success", "https://example.com/cancel");

            Assert.Null(result);
            world.Stripe.Verify(x => x.CreateCheckoutAsync(It.IsAny<SessionCreateOptions>()), Times.Never);
        }

        // "Retry Payment" used to leave the first page open. A customer with both open could
        // pay on each.
        [Fact]
        public async Task ASecondTryClosesThePageOfTheFirst()
        {
            using var world = new BookingWorld(Key);
            var firstPage = await world.SeedPendingPaymentAsync("12 Main Rd, Sutton");
            Booking booking = world.DbContext.Bookings.Single();
            world.Stripe.Setup(x => x.ExpireCheckoutAsync(firstPage)).ReturnsAsync(new Session { Id = firstPage, Status = "expired", PaymentStatus = "unpaid" });
            world.Stripe
                .Setup(x => x.CreateCheckoutAsync(It.IsAny<SessionCreateOptions>()))
                .ReturnsAsync(new Session { Id = "cs_test_second", Url = "https://checkout.stripe.com/c/pay/cs_test_second" });

            PaymentCheckoutResult result = await world.Payments.CreateCheckoutSessionAsync(booking.Id, "https://example.com/success", "https://example.com/cancel");

            Assert.Equal("cs_test_second", result.SessionId);
            world.Stripe.Verify(x => x.ExpireCheckoutAsync(firstPage), Times.Once);
            Assert.Equal("Cancelled", world.DbContext.Payments.Single(x => x.CheckoutSessionId == firstPage).Status.Name);
            Assert.Equal("Pending", world.DbContext.Payments.Single(x => x.CheckoutSessionId == "cs_test_second").Status.Name);
        }

        [Fact]
        public async Task ASecondTryOpensNoNewPageWhenTheFirstTurnsOutToBePaid()
        {
            using var world = new BookingWorld(Key);
            var firstPage = await world.SeedPendingPaymentAsync("12 Main Rd, Sutton");
            Booking booking = world.DbContext.Bookings.Single();
            world.Stripe.Setup(x => x.ExpireCheckoutAsync(firstPage)).ReturnsAsync(Paid(firstPage, "pi_first"));

            PaymentCheckoutResult result = await world.Payments.CreateCheckoutSessionAsync(booking.Id, "https://example.com/success", "https://example.com/cancel");

            Assert.Null(result);
            Assert.Equal("Approved", world.StatusOf(booking));
            world.Stripe.Verify(x => x.CreateCheckoutAsync(It.IsAny<SessionCreateOptions>()), Times.Never);
        }

        // The site gives an unpaid booking's hour away after fifteen minutes while Stripe kept
        // its page open for a day: a customer could pay for a booking the site had dropped. The
        // page is now closed at Stripe before the hour goes back on sale.
        [Fact]
        public async Task TheSweepClosesTheStripePageBeforeItDropsABooking()
        {
            using var world = new BookingWorld(Key);
            var sessionId = await world.SeedPendingPaymentAsync("12 Main Rd, Sutton");
            Booking booking = await MadeAnHourAgoAsync(world);
            world.Stripe.Setup(x => x.ExpireCheckoutAsync(sessionId)).ReturnsAsync(new Session { Id = sessionId, Status = "expired", PaymentStatus = "unpaid" });

            var dropped = await world.Bookings.ReleaseAbandonedBookingsAsync(TimeSpan.FromMinutes(15));

            Assert.Equal(1, dropped);
            world.Stripe.Verify(x => x.ExpireCheckoutAsync(sessionId), Times.Once);
            Assert.Equal("Abandoned", world.StatusOf(booking));
            Assert.Equal(new[] { "Cancelled" }, world.PaymentStatusesOf(booking));
            Assert.False(world.Slot(world.SlotId).IsBooked);
        }

        // The customer paid in the last moment before the sweep came round. Stripe refuses to
        // close a paid page and says so; that is a booking, and it keeps its hour.
        [Fact]
        public async Task TheSweepKeepsABookingWhoseStripePageTurnsOutToBePaid()
        {
            using var world = new BookingWorld(Key);
            List<SentEmail> sent = world.CaptureEmails();
            var sessionId = await world.SeedPendingPaymentAsync("12 Main Rd, Sutton");
            Booking booking = await MadeAnHourAgoAsync(world);
            world.Stripe.Setup(x => x.ExpireCheckoutAsync(sessionId)).ReturnsAsync(Paid(sessionId, "pi_just_in_time"));

            var dropped = await world.Bookings.ReleaseAbandonedBookingsAsync(TimeSpan.FromMinutes(15));

            Assert.Equal(0, dropped);
            Assert.Equal("Approved", world.StatusOf(booking));
            Assert.Equal(new[] { "DepositPaid" }, world.PaymentStatusesOf(booking));
            Assert.True(world.Slot(world.SlotId).IsBooked);
            Assert.Equal(booking.Id, world.Slot(world.SlotId).BookingId);
            Assert.Equal(2, sent.Count);
        }

        // If Stripe cannot be reached the page may still be open, so the booking keeps its hour
        // and the next run tries again. Dropping it here is what would let it be paid for late.
        [Fact]
        public async Task TheSweepLeavesABookingAloneWhileStripeCannotBeReached()
        {
            using var world = new BookingWorld(Key);
            var sessionId = await world.SeedPendingPaymentAsync("12 Main Rd, Sutton");
            Booking booking = await MadeAnHourAgoAsync(world);
            world.Stripe.Setup(x => x.ExpireCheckoutAsync(sessionId)).ThrowsAsync(new TimeoutException("Stripe did not answer"));

            var dropped = await world.Bookings.ReleaseAbandonedBookingsAsync(TimeSpan.FromMinutes(15));

            Assert.Equal(0, dropped);
            Assert.Equal("Pending", world.StatusOf(booking));
            Assert.Equal(new[] { "Pending" }, world.PaymentStatusesOf(booking));
            Assert.True(world.Slot(world.SlotId).IsBooked);
        }

        // An admin can cancel a booking that is still waiting for its deposit. Its page was
        // left open at Stripe, for the customer to go on and pay.
        [Fact]
        public async Task CancellingABookingThatWaitsForItsDepositClosesItsStripePage()
        {
            using var world = new BookingWorld(Key);
            var sessionId = await world.SeedPendingPaymentAsync("12 Main Rd, Sutton");
            Booking booking = world.DbContext.Bookings.Single();
            world.Stripe.Setup(x => x.ExpireCheckoutAsync(sessionId)).ReturnsAsync(new Session { Id = sessionId, Status = "expired", PaymentStatus = "unpaid" });

            Assert.True(await world.Bookings.CancelBookingAsync(booking.Id, "The customer rang to call it off"));

            world.Stripe.Verify(x => x.ExpireCheckoutAsync(sessionId), Times.Once);
            Assert.Equal("Cancelled", world.StatusOf(booking));
            Assert.Equal(new[] { "Cancelled" }, world.PaymentStatusesOf(booking));
        }

        // Money that arrives for a booking the site has dropped used to switch it back on,
        // "confirmed", with no hour held: its slot could be another customer's by then. Now the
        // money is written on the job, the booking stays as it was, and the company is told.
        [Theory]
        [InlineData("Abandoned")]
        [InlineData("Cancelled")]
        public async Task MoneyForABookingThatIsNotWaitingForItIsWrittenDownAndSwitchesNothingOn(string state)
        {
            using var world = new BookingWorld(Key);
            List<SentEmail> sent = world.CaptureEmails();
            var sessionId = await world.SeedPendingPaymentAsync("12 Main Rd, Sutton");
            Booking booking = world.DbContext.Bookings.Single();
            world.SetStatus(booking, state);
            world.DbContext.Payments.Single().StatusId = world.DbContext.PaymentStatuses.Single(s => s.Name == "Cancelled").Id;
            await world.DbContext.SaveChangesAsync();
            StripeAnswers(world, sessionId, paid: true, paymentIntentId: "pi_late");

            await world.Payments.ConfirmCheckoutAsync(sessionId);

            Assert.Equal(state, world.StatusOf(booking));
            Assert.Equal(new[] { "DepositPaid" }, world.PaymentStatusesOf(booking));
            Assert.Equal("pi_late", world.DbContext.Payments.Single().TransactionId);
            Assert.Contains(world.HistoryOf(booking), line => line.Contains("when the booking was not waiting for one"));

            // One email, to the company. The customer is not told the booking is confirmed.
            SentEmail notice = Assert.Single(sent);
            Assert.StartsWith("Action needed: deposit paid for a booking that is not held", notice.Subject);
            Assert.NotEqual("ada@example.com", notice.To);
            Assert.Equal("ada@example.com", notice.ReplyTo);
        }

        [Theory]
        [InlineData("Pending", true, false, true)]
        [InlineData("Pending", true, true, false)]
        [InlineData("Pending", false, false, false)]
        [InlineData("Approved", true, false, false)]
        [InlineData("Abandoned", true, false, false)]
        [InlineData("Cancelled", true, false, false)]
        public void OnlyAWebsiteBookingStillWaitingForItsDepositCanPayIt(string status, bool cameFromWebsite, bool depositPaid, bool expected)
        {
            Assert.Equal(expected, BookingRules.CanPayDeposit(status, cameFromWebsite, depositPaid));
        }

        // What the customer's "Booking Confirmed" page stands on. It used to show for any id.
        [Theory]
        [InlineData("Approved", true, true, true)]
        [InlineData("Completed", true, true, true)]
        [InlineData("Pending", true, false, false)]
        [InlineData("Approved", true, false, false)]
        [InlineData("Abandoned", true, true, false)]
        [InlineData("Cancelled", true, true, false)]
        [InlineData("Approved", false, false, false)]
        public void ABookingIsConfirmedWhenItsDepositIsInAndItIsOnOrDone(string status, bool cameFromWebsite, bool depositPaid, bool expected)
        {
            Assert.Equal(expected, BookingRules.IsConfirmed(status, cameFromWebsite, depositPaid));
        }

        // Stripe's message says which page; the site still asks Stripe whether it was paid, so
        // the two ways a payment is heard of cannot disagree.
        [Fact]
        public async Task AMessageFromStripeThatAPageWasPaidConfirmsTheBookingAfterAskingStripe()
        {
            using var world = new BookingWorld(Key);
            var sessionId = await world.SeedPendingPaymentAsync("12 Main Rd, Sutton");
            Booking booking = world.DbContext.Bookings.Single();
            StripeSends(world, EventTypes.CheckoutSessionCompleted, sessionId);
            StripeAnswers(world, sessionId, paid: true, paymentIntentId: "pi_from_webhook");

            await world.Payments.HandleWebhookEventAsync("{}", "signature");

            Assert.Equal("Approved", world.StatusOf(booking));
            Assert.Equal("pi_from_webhook", world.DbContext.Payments.Single().TransactionId);
            world.Stripe.Verify(x => x.GetCheckoutAsync(sessionId), Times.Once);
        }

        [Fact]
        public async Task AMessageFromStripeThatAPageExpiredGivesThePaymentUp()
        {
            using var world = new BookingWorld(Key);
            var sessionId = await world.SeedPendingPaymentAsync("12 Main Rd, Sutton");
            Booking booking = world.DbContext.Bookings.Single();
            StripeSends(world, EventTypes.CheckoutSessionExpired, sessionId);

            await world.Payments.HandleWebhookEventAsync("{}", "signature");

            Assert.Equal(new[] { "Cancelled" }, world.PaymentStatusesOf(booking));
            Assert.Equal("Pending", world.StatusOf(booking));
        }

        // A new Stripe account sends its messages in a newer format than the library on the
        // site was built for, and the library refused every one of them for that alone. What
        // proves a message is Stripe's is its signature, and that is still checked.
        [Fact]
        public void AMessageSignedByStripeIsReadWhateverVersionOfItsFormatItIsIn()
        {
            const string secret = "whsec_test_secret";
            const string json = "{\"id\":\"evt_test_1\",\"object\":\"event\",\"api_version\":\"2099-01-01.zinnia\",\"type\":\"checkout.session.completed\",\"data\":{\"object\":{\"id\":\"cs_test_abc\",\"object\":\"checkout.session\",\"payment_status\":\"paid\"}}}";
            var gateway = new StripeGateway(new ConfigurationBuilder().Build());

            Event read = gateway.ReadEvent(json, SignatureFor(json, secret), secret);

            Assert.Equal(EventTypes.CheckoutSessionCompleted, read.Type);
            Assert.Equal("cs_test_abc", Assert.IsType<Session>(read.Data.Object).Id);
        }

        [Fact]
        public void AMessageNotSignedByStripeIsRefused()
        {
            const string json = "{\"id\":\"evt_forged\",\"object\":\"event\",\"type\":\"checkout.session.completed\",\"data\":{\"object\":{\"id\":\"cs_test_abc\",\"object\":\"checkout.session\"}}}";
            var gateway = new StripeGateway(new ConfigurationBuilder().Build());

            Assert.Throws<StripeException>(() => gateway.ReadEvent(json, SignatureFor(json, "whsec_somebody_elses"), "whsec_test_secret"));
            Assert.ThrowsAny<Exception>(() => gateway.ReadEvent(json, string.Empty, "whsec_test_secret"));
        }

        private static void StripeAnswers(BookingWorld world, string sessionId, bool paid, string paymentIntentId = null)
        {
            world.Stripe
                .Setup(x => x.GetCheckoutAsync(sessionId))
                .ReturnsAsync(paid ? Paid(sessionId, paymentIntentId) : new Session { Id = sessionId, Status = "open", PaymentStatus = "unpaid" });
        }

        private static Session Paid(string sessionId, string paymentIntentId)
        {
            return new Session { Id = sessionId, Status = "complete", PaymentStatus = "paid", PaymentIntentId = paymentIntentId };
        }

        private static void StripeSends(BookingWorld world, string eventType, string sessionId)
        {
            world.Stripe
                .Setup(x => x.ReadEvent(It.IsAny<string>(), It.IsAny<string>(), "whsec_test"))
                .Returns(new Event { Type = eventType, Data = new EventData { Object = new Session { Id = sessionId } } });
        }

        // A website booking waiting for its deposit, before any payment page was opened for it.
        private static async Task<Booking> SeedBookingWithNoPageYetAsync(BookingWorld world)
        {
            await world.SeedPendingPaymentAsync("12 Main Rd, Sutton");
            world.DbContext.Payments.RemoveRange(world.DbContext.Payments);
            await world.DbContext.SaveChangesAsync();
            return world.DbContext.Bookings.Single();
        }

        private static async Task<Booking> MadeAnHourAgoAsync(BookingWorld world)
        {
            Booking booking = world.DbContext.Bookings.Single();
            booking.CreatedOn = DateTime.UtcNow.AddHours(-1);
            await world.DbContext.SaveChangesAsync();
            return booking;
        }

        // The header Stripe sends with a message: when it was signed, and the signature.
        private static string SignatureFor(string json, string secret)
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes($"{timestamp}.{json}"));
            return $"t={timestamp},v1={Convert.ToHexString(hash).ToLowerInvariant()}";
        }
    }
}
