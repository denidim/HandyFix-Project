namespace HandyFix.Services.Data.Tests
{
    using System;

    using HandyFix.Common;

    using Xunit;

    // The reference a booking goes by wherever a person reads it (PROJECT_STATE.md Section 3ce).
    // The admin pages showed the first eight characters of its id while the emails and the
    // customer's page showed all 36, so the two could not be matched by eye or read out by phone.
    public class BookingReferenceTests
    {
        [Fact]
        public void ShortShouldBeTheFirstEightCharactersOfTheIdInCapitals()
        {
            Assert.Equal("A7C30F12", BookingReference.Short(Guid.Parse("a7c30f12-5b1e-4c7d-9a10-3f2e8d6b4c01")));
        }

        // What the admin pages printed before there was one rule for it. A reference already
        // written down, or searched for in the admin list, has to stay the same.
        [Fact]
        public void ShortShouldMatchWhatTheAdminPagesAlwaysShowed()
        {
            var id = Guid.NewGuid();

            Assert.Equal(id.ToString().Substring(0, 8).ToUpper(), BookingReference.Short(id));
        }
    }
}
