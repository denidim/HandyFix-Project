namespace HandyFix.Services.Data.Tests
{
    using HandyFix.Common;

    using Xunit;

    public class DepositTests
    {
        // The pages print the deposit from Deposit. Those that say "£50" drop the pence, which is
        // only true while the amount is a whole number of pounds.
        [Fact]
        public void TheDepositShouldBeAWholeNumberOfPoundsWrittenBothWays()
        {
            Assert.Equal(50.00m, Deposit.Amount);
            Assert.Equal("50", Deposit.Pounds);
            Assert.Equal("50.00", Deposit.PoundsAndPence);
            Assert.Equal(Deposit.Pounds + ".00", Deposit.PoundsAndPence);
        }
    }
}
