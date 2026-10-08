namespace HandyFix.Web.Tests.Validation
{
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.Linq;

    using HandyFix.Web.ViewModels.Booking;
    using HandyFix.Web.ViewModels.Home;
    using HandyFix.Web.ViewModels.ServiceAreas;
    using HandyFix.Web.ViewModels.Validation;

    using Xunit;

    // The stricter rules on the public forms (PROJECT_STATE.md Section 3cb). The browser runs the
    // same patterns through jQuery Validation; the cases here are the ones the patterns were
    // checked against in JavaScript too.
    public class ValidationRulesTests
    {
        [Theory]
        [InlineData("07123456789")]
        [InlineData("07123 456789")]
        [InlineData("07123-456-789")]
        [InlineData("+447123456789")]
        [InlineData("+44 7123 456789")]
        [InlineData("0044 7123 456789")]
        [InlineData("+44 (0)7123 456789")]
        [InlineData("020 3951 5915")]
        [InlineData("+44 20 3951 5915")]
        [InlineData("01372 123456")]
        [InlineData("0137212345")]
        [InlineData("0330 123 4567")]
        [InlineData(" 07123456789 ")]
        public void UkPhoneShouldAcceptUkMobilesAndLandlines(string phone)
        {
            Assert.True(new UkPhoneAttribute().IsValid(phone));
        }

        [Theory]
        [InlineData("7123456789")]
        [InlineData("0712345678")]
        [InlineData("071234567890")]
        [InlineData("08001234567")]
        [InlineData("09123456789")]
        [InlineData("05123456789")]
        [InlineData("+1 202 555 0143")]
        [InlineData("12345")]
        [InlineData("call me")]
        [InlineData("0712345678a")]

        // Digits from another script: \d would have let these through on the server only.
        [InlineData("٠٧١٢٣٤٥٦٧٨٩")]
        public void UkPhoneShouldRefuseAnythingElse(string phone)
        {
            Assert.False(new UkPhoneAttribute().IsValid(phone));
        }

        [Theory]
        [InlineData("Jane")]
        [InlineData("Jane Doe")]
        [InlineData("Mary-Jane O'Neil")]
        [InlineData("D’Arcy")]
        [InlineData("Zoë")]
        [InlineData("François")]
        [InlineData("Łukasz")]
        [InlineData("de la Cruz")]
        [InlineData(" Jane")]
        public void PersonNameShouldAcceptLettersWithSpacesHyphensAndApostrophes(string name)
        {
            Assert.True(new PersonNameAttribute().IsValid(name));
        }

        [Theory]
        [InlineData("Jane2")]
        [InlineData("http://spam.example")]
        [InlineData("Jane@Doe")]
        [InlineData("123")]
        [InlineData("-Jane")]
        [InlineData("Jane_Doe")]
        [InlineData("Jane.Doe")]
        public void PersonNameShouldRefuseDigitsSymbolsAndLinks(string name)
        {
            Assert.False(new PersonNameAttribute().IsValid(name));
        }

        [Theory]
        [InlineData("jane@example.com")]
        [InlineData("jane.doe+tag@mail.example.co.uk")]
        [InlineData("j@x.io")]
        public void StrictEmailShouldAcceptAnAddressWithAFullDomain(string email)
        {
            Assert.True(new StrictEmailAttribute().IsValid(email));
        }

        [Theory]
        [InlineData("jane@gmail")]
        [InlineData("jane")]
        [InlineData("jane@")]
        [InlineData("@example.com")]
        [InlineData("jane doe@example.com")]
        [InlineData("jane@example.c")]
        [InlineData("jane@@example.com")]
        public void StrictEmailShouldRefuseAnAddressThatCannotBeRepliedTo(string email)
        {
            Assert.False(new StrictEmailAttribute().IsValid(email));
        }

        // Why the forms stopped using [EmailAddress]: it only asks for an "@" with something on
        // each side.
        [Fact]
        public void TheBuiltInEmailRuleAcceptsAnAddressWithNoDomainEnding()
        {
            Assert.True(new EmailAddressAttribute().IsValid("jane@gmail"));
        }

        [Theory]
        [InlineData("KT9 2QN")]
        [InlineData("kt92qn")]
        [InlineData("SW19 5AB")]
        [InlineData("SW1A 1AA")]
        [InlineData("M1 1AE")]
        [InlineData(" KT9 2QN ")]
        public void UkPostcodeShouldAcceptAWholePostcode(string postcode)
        {
            Assert.True(new UkPostcodeAttribute().IsValid(postcode));
            Assert.True(UkPostcode.IsValid(postcode));
        }

        [Theory]
        [InlineData("KT9")]
        [InlineData("KT9 2Q")]
        [InlineData("KT 9 2QN")]
        [InlineData("12345")]
        [InlineData("KT9-2QN")]
        [InlineData("KTT9 2QN")]
        public void UkPostcodeShouldRefuseAPartOrSomethingElse(string postcode)
        {
            Assert.False(new UkPostcodeAttribute().IsValid(postcode));
            Assert.False(UkPostcode.IsValid(postcode));
        }

        [Theory]
        [InlineData("kt92qn", "KT9 2QN", "KT9")]
        [InlineData(" KT9 2QN ", "KT9 2QN", "KT9")]
        [InlineData("sw195ab", "SW19 5AB", "SW19")]
        [InlineData("SW1A 1AA", "SW1A 1AA", "SW1A")]
        [InlineData("m1 1ae", "M1 1AE", "M1")]
        public void UkPostcodeShouldNormalizeAndGiveTheDistrict(string postcode, string normalized, string district)
        {
            Assert.Equal(normalized, UkPostcode.Normalize(postcode));
            Assert.Equal(district, UkPostcode.GetOutwardCode(postcode));
        }

        [Fact]
        public void UkPostcodeShouldGiveNothingForWhatIsNotAPostcode()
        {
            Assert.Null(UkPostcode.Normalize("KT9"));
            Assert.Null(UkPostcode.GetOutwardCode(null));
        }

        [Fact]
        public void ParseDistrictsShouldTidyAListAndDropWhatIsNotADistrict()
        {
            Assert.Equal(new[] { "KT5", "KT6", "SW19" }, UkPostcode.ParseDistricts("kt5,  KT6 ,sw19, kt5, Chessington, "));
            Assert.Empty(UkPostcode.ParseDistricts("  "));
            Assert.Empty(UkPostcode.ParseDistricts(null));
        }

        [Theory]
        [InlineData("KT9 2QN", true)]
        [InlineData("kt101aa", true)]

        // KT1 is not in the list just because KT10 and KT19 are.
        [InlineData("KT1 1AA", false)]
        [InlineData("KT19 1AA", true)]
        [InlineData("M1 1AE", false)]

        // A lettered London district belongs to its district.
        [InlineData("SW1A 1AA", true)]
        [InlineData("SW19 1AA", false)]
        [InlineData("not a postcode", false)]
        public void IsInDistrictsShouldMatchTheWholeDistrict(string postcode, bool expected)
        {
            var districts = new[] { "KT9", "KT10", "KT19", "SW1" };

            Assert.Equal(expected, UkPostcode.IsInDistricts(postcode, districts));
        }

        [Theory]
        [InlineData("My kitchen tap drips and the cold one is stiff.")]

        // One link with a sentence around it is an ordinary message.
        [InlineData("Please fit this tap: https://www.example.com/products/taps/a-very-long-product-address-12345?colour=chrome")]
        [InlineData("Three to compare www.a.example www.b.example www.c.example")]
        [InlineData("")]
        [InlineData(null)]
        public void NotMostlyLinksShouldAcceptAMessageInTheVisitorsOwnWords(string message)
        {
            Assert.True(new NotMostlyLinksAttribute().IsValid(message));
        }

        [Theory]
        [InlineData("https://spam.example/buy-now")]
        [InlineData("Hi https://spam.example")]
        [InlineData("Lots of useful links for you: http://a.example http://b.example http://c.example http://d.example")]
        [InlineData("WWW.SPAM.EXAMPLE")]
        public void NotMostlyLinksShouldRefuseAMessageThatIsLinksAndLittleElse(string message)
        {
            Assert.False(new NotMostlyLinksAttribute().IsValid(message));
        }

        [Theory]
        [InlineData("KT9")]
        [InlineData("kt5, KT6,KT7")]
        [InlineData(" SW19 , SW20 ")]
        [InlineData("")]
        public void AnAreasPostcodeDistrictsShouldBeAListOfDistrictsOrEmpty(string districts)
        {
            Assert.Empty(ErrorsFor(AreaWithDistricts(districts), nameof(ServiceAreaAdminInputModel.PostcodeDistricts)));
        }

        [Theory]
        [InlineData("Chessington")]
        [InlineData("KT9 KT10")]
        [InlineData("KT9 2QN")]
        [InlineData("KT9,")]
        public void AnAreasPostcodeDistrictsShouldRefuseAnythingElse(string districts)
        {
            Assert.Single(ErrorsFor(AreaWithDistricts(districts), nameof(ServiceAreaAdminInputModel.PostcodeDistricts)));
        }

        // The rules above are only worth something on the forms. One bad value per field, each
        // on the form it is typed into.
        [Fact]
        public void TheBookingFormShouldApplyTheRules()
        {
            var model = new BookingInputModel
            {
                CustomerFirstName = "Ada1",
                CustomerLastName = "Love_lace",
                Email = "ada@example",
                PhoneNumber = "12345",
                Address = "1 Ash Rd",
                Postcode = "KT9",
                ProblemDescription = "Tap drips.",
            };

            Assert.Equal(
                new[]
                {
                    nameof(BookingInputModel.CustomerFirstName),
                    nameof(BookingInputModel.CustomerLastName),
                    nameof(BookingInputModel.Email),
                    nameof(BookingInputModel.PhoneNumber),
                    nameof(BookingInputModel.Postcode),
                    nameof(BookingInputModel.ProblemDescription),
                },
                InvalidFields(model));
        }

        [Fact]
        public void TheBookingFormShouldAcceptAnOrdinaryBooking()
        {
            var model = new BookingInputModel
            {
                CustomerFirstName = "Ada",
                CustomerLastName = "Lovelace",
                Email = "ada@example.com",
                PhoneNumber = "07700 900123",
                Address = "1 Ash Rd",
                Postcode = "kt9 1aa",
                ProblemDescription = "The kitchen tap has been dripping for a week.",
            };

            Assert.Empty(InvalidFields(model));
        }

        [Fact]
        public void TheContactFormShouldApplyTheRules()
        {
            var model = new ContactInputModel
            {
                Name = "www.spam.example",
                Email = "jane@gmail",
                PhoneNumber = "+1 202 555 0143",
                Message = "https://spam.example/buy-now",
                Category = "Plumbing",
            };

            Assert.Equal(
                new[]
                {
                    nameof(ContactInputModel.Name),
                    nameof(ContactInputModel.Email),
                    nameof(ContactInputModel.PhoneNumber),
                    nameof(ContactInputModel.Message),
                },
                InvalidFields(model));
        }

        [Fact]
        public void TheJoinOurTeamFormShouldApplyTheRules()
        {
            var model = new JoinTeamInputModel
            {
                Name = "Jane 4 Hire",
                Email = "jane@",
                PhoneNumber = "0800 123 4567",
                Trade = "Plumbing",
                YearsExperience = 5,
                Availability = "Full-time",
                AboutYou = "http://a.example http://b.example http://c.example http://d.example",
            };

            Assert.Equal(
                new[]
                {
                    nameof(JoinTeamInputModel.Name),
                    nameof(JoinTeamInputModel.Email),
                    nameof(JoinTeamInputModel.PhoneNumber),
                    nameof(JoinTeamInputModel.AboutYou),
                },
                InvalidFields(model));
        }

        private static ServiceAreaAdminInputModel AreaWithDistricts(string districts)
        {
            return new ServiceAreaAdminInputModel
            {
                Name = "Chessington",
                Slug = "chessington",
                Region = "Home Turf",
                IntroCopy = "Chessington is where it all starts for us.",
                LocalNeighbourhoodsCopy = "From Hook to Malden Rushett and Garrison Lane.",
                PostcodeDistricts = districts,
            };
        }

        private static IEnumerable<ValidationResult> ErrorsFor(object model, string field)
        {
            var results = new List<ValidationResult>();
            Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
            return results.Where(r => r.MemberNames.Contains(field));
        }

        // The fields with an error, in the order the model declares them.
        private static IEnumerable<string> InvalidFields(object model)
        {
            var results = new List<ValidationResult>();
            Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);

            var invalid = results.SelectMany(r => r.MemberNames).Distinct().ToList();
            return model.GetType().GetProperties().Select(p => p.Name).Where(invalid.Contains);
        }
    }
}
