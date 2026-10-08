namespace HandyFix.Web.Tests.Services
{
    using System.Net;

    using HandyFix.Web.Controllers;
    using HandyFix.Web.Services.Forms;
    using HandyFix.Web.ViewModels;

    using Microsoft.AspNetCore.Mvc;

    using Xunit;

    // Who counts as one visitor for the limit on sending forms, and what the status page says
    // (PROJECT_STATE.md Section 3cb). The limit itself runs through the whole stack in
    // StatusPagesWebTests.
    public class RateLimitsTests
    {
        [Theory]
        [InlineData("203.0.113.7", "203.0.113.7")]

        // The same IPv4 address, as a server that listens on IPv6 sees it.
        [InlineData("::ffff:203.0.113.7", "203.0.113.7")]
        public void ClientKeyShouldBeTheAddressForIPv4(string address, string expected)
        {
            Assert.Equal(expected, RateLimits.ClientKey(IPAddress.Parse(address)));
        }

        // A provider gives one customer the first half of an IPv6 address; the second half is
        // the customer's to change, and would be a fresh allowance each time if it counted.
        [Fact]
        public void ClientKeyShouldBeTheFirstHalfOfAnIPv6Address()
        {
            var one = RateLimits.ClientKey(IPAddress.Parse("2001:db8:1234:5678:aaaa:bbbb:cccc:dddd"));
            var sameCustomer = RateLimits.ClientKey(IPAddress.Parse("2001:db8:1234:5678:1:2:3:4"));
            var anotherCustomer = RateLimits.ClientKey(IPAddress.Parse("2001:db8:1234:5679:aaaa:bbbb:cccc:dddd"));

            Assert.Equal("2001:db8:1234:5678::/64", one);
            Assert.Equal(one, sameCustomer);
            Assert.NotEqual(one, anotherCustomer);
        }

        [Fact]
        public void ClientKeyShouldStillCountARequestWithNoAddress()
        {
            Assert.Equal("unknown", RateLimits.ClientKey(null));
        }

        [Theory]
        [InlineData(404, 404, "Page Not Found")]
        [InlineData(429, 429, "Too Many Attempts")]
        [InlineData(400, 400, "That Didn't Go Through")]
        [InlineData(403, 403, "No Access")]
        [InlineData(500, 500, "Something Went Wrong")]
        [InlineData(502, 502, "Something Went Wrong")]
        [InlineData(418, 418, "That Didn't Work")]
        public void StatusPageViewModelShouldHaveWordsForEachStatus(int code, int expectedCode, string title)
        {
            StatusPageViewModel model = StatusPageViewModel.For(code);

            Assert.Equal(expectedCode, model.Code);
            Assert.Equal(title, model.Title);
            Assert.NotEmpty(model.Heading);
            Assert.NotEmpty(model.Message);
        }

        [Theory]
        [InlineData(404, 404)]
        [InlineData(429, 429)]
        [InlineData(500, 500)]

        // Not an error status: opened by its own address, it is a page that is not there.
        [InlineData(200, 404)]
        [InlineData(302, 404)]
        [InlineData(-1, 404)]
        [InlineData(600, 404)]
        public void StatusPageShouldAnswerWithTheStatusItDescribes(int code, int expected)
        {
            var result = new ErrorsController().StatusPage(code);

            ViewResult page = Assert.IsType<ViewResult>(result);
            Assert.Equal("StatusPage", page.ViewName);
            Assert.Equal(expected, page.StatusCode);
            Assert.Equal(expected, Assert.IsType<StatusPageViewModel>(page.Model).Code);
        }
    }
}
