namespace HandyFix.Web.Tests.Services
{
    using System.Collections.Generic;
    using System.Linq;

    using HandyFix.Common;
    using HandyFix.Web.Services;
    using HandyFix.Web.Settings;

    using Microsoft.Extensions.Options;

    using Xunit;

    // The one record of the business that every page writes for Google (PROJECT_STATE Section 3eg).
    public class BusinessRecordTests
    {
        private const string GoogleProfile = "https://g.page/r/example";

        [Fact]
        public void TheWholeRecordCarriesTheContextAndTheBusiness()
        {
            var record = Create(GoogleProfile).Build();

            Assert.Equal("https://schema.org", record["@context"]);
            Assert.Equal("@context", record.Keys.First());
            Assert.Equal("Plumber", record["@type"]);
            Assert.Equal(GlobalConstants.SiteUrl + "/#business", record["@id"]);
            Assert.Equal(GlobalConstants.SystemName, record["name"]);
            Assert.Equal(GlobalConstants.CompanyLegalName, record["legalName"]);
            Assert.Equal(GlobalConstants.BusinessPhoneInternational, record["telephone"]);
            Assert.Equal(GlobalConstants.BusinessEmail, record["email"]);

            var address = (Dictionary<string, object>)record["address"];
            Assert.Equal(GlobalConstants.CompanyAddressPostcode, address["postalCode"]);
            Assert.Equal("GB", address["addressCountry"]);
        }

        [Fact]
        public void TheProviderFormIsTheSameRecordWithoutTheContext()
        {
            var business = Create(GoogleProfile);
            var whole = business.Build();
            var provider = business.BuildAsProvider();

            Assert.False(provider.ContainsKey("@context"));
            Assert.Equal(whole.Keys.Where(key => key != "@context"), provider.Keys);
        }

        [Fact]
        public void TheRecordIsOpenAllWeekRoundTheClockAndNamesTheTwentyTowns()
        {
            var record = Create(GoogleProfile).BuildAsProvider();

            var hours = (Dictionary<string, object>)record["openingHoursSpecification"];
            Assert.Equal(7, ((string[])hours["dayOfWeek"]).Length);
            Assert.Equal("00:00", hours["opens"]);
            Assert.Equal("23:59", hours["closes"]);

            var towns = (List<Dictionary<string, object>>)record["areaServed"];
            Assert.Equal(20, towns.Count);
            Assert.All(towns, town => Assert.Equal("City", town["@type"]));
            Assert.Equal(ServiceAreaTowns.Names, towns.Select(town => (string)town["name"]));
        }

        [Fact]
        public void TheGoogleProfileIsInSameAsWhenItIsSet()
        {
            var record = Create(GoogleProfile).BuildAsProvider();

            Assert.Equal(new[] { GoogleProfile }, (List<string>)record["sameAs"]);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("  ")]
        public void SameAsIsLeftOutWhenThereIsNoGoogleProfile(string link)
        {
            var record = Create(link).BuildAsProvider();

            Assert.False(record.ContainsKey("sameAs"));
        }

        private static BusinessRecord Create(string googleReviewsUrl)
        {
            return new BusinessRecord(Options.Create(new BusinessOptions { GoogleReviewsUrl = googleReviewsUrl }));
        }
    }
}
