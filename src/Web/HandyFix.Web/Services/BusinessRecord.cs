namespace HandyFix.Web.Services
{
    using System.Collections.Generic;
    using System.Linq;

    using HandyFix.Common;
    using HandyFix.Web.Settings;

    using Microsoft.Extensions.Options;

    public class BusinessRecord : IBusinessRecord
    {
        // Where the business is, for the registered office's postcode. The numbers the home page
        // used to carry were about 1.4 km off.
        private const double Latitude = 51.364066;

        private const double Longitude = -0.314436;

        // The logo has to be a file Google can fetch; the one in the header is drawn inline.
        private const string LogoPath = "/apple-touch-icon.png";

        private static readonly string[] Days =
        {
            "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday",
        };

        private readonly BusinessOptions business;

        public BusinessRecord(IOptions<BusinessOptions> business)
        {
            this.business = business.Value;
        }

        public Dictionary<string, object> Build()
        {
            // "@context" is put first so it reads first in the written record.
            var withContext = new Dictionary<string, object> { ["@context"] = "https://schema.org" };
            foreach (var pair in this.BuildAsProvider())
            {
                withContext[pair.Key] = pair.Value;
            }

            return withContext;
        }

        public Dictionary<string, object> BuildAsProvider()
        {
            var record = new Dictionary<string, object>
            {
                ["@type"] = "Plumber",
                ["@id"] = GlobalConstants.SiteUrl + "/#business",
                ["name"] = GlobalConstants.SystemName,
                ["legalName"] = GlobalConstants.CompanyLegalName,
                ["foundingDate"] = GlobalConstants.CompanyIncorporatedOn,
                ["url"] = GlobalConstants.SiteUrl,
                ["logo"] = GlobalConstants.SiteUrl + LogoPath,
                ["telephone"] = GlobalConstants.BusinessPhoneInternational,
                ["email"] = GlobalConstants.BusinessEmail,
                ["priceRange"] = "££",
                ["address"] = new Dictionary<string, object>
                {
                    ["@type"] = "PostalAddress",
                    ["streetAddress"] = GlobalConstants.CompanyAddressStreet,
                    ["addressLocality"] = GlobalConstants.CompanyAddressTown,
                    ["addressRegion"] = GlobalConstants.CompanyAddressCounty,
                    ["postalCode"] = GlobalConstants.CompanyAddressPostcode,
                    ["addressCountry"] = "GB",
                },
                ["geo"] = new Dictionary<string, object>
                {
                    ["@type"] = "GeoCoordinates",
                    ["latitude"] = Latitude,
                    ["longitude"] = Longitude,
                },

                // Open round the clock, as on the profile: the phone is answered at any hour.
                ["openingHoursSpecification"] = new Dictionary<string, object>
                {
                    ["@type"] = "OpeningHoursSpecification",
                    ["dayOfWeek"] = Days,
                    ["opens"] = "00:00",
                    ["closes"] = "23:59",
                },
                ["areaServed"] = this.AreaServed(),
            };

            // The profile's link is a setting, so a profile that moves is an edit there.
            if (!string.IsNullOrWhiteSpace(this.business.GoogleReviewsUrl))
            {
                record["sameAs"] = new List<string> { this.business.GoogleReviewsUrl };
            }

            return record;
        }

        // The towns of the profile's service area as search engines read them, the same on every page.
        public List<Dictionary<string, object>> AreaServed()
        {
            return ServiceAreaTowns.Names
                .Select(name => new Dictionary<string, object> { ["@type"] = "City", ["name"] = name })
                .ToList();
        }
    }
}
