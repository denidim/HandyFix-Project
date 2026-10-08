namespace HandyFix.Web.ViewModels.Validation
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;

    // A UK postcode is an outward code and an inward code: "KT9 2QN" is "KT9", the district, and
    // "2QN". The booking form asks for the whole postcode and the site only takes bookings in the
    // districts its service areas list (PROJECT_STATE.md Section 3cb).
    public static class UkPostcode
    {
        // Written with plain character classes and no named groups or look-behind, so the same
        // pattern runs in the browser: the validation attributes hand it to jQuery Validation.
        public const string Pattern = @"^ *[A-Za-z]{1,2}[0-9][A-Za-z0-9]? ?[0-9][A-Za-z]{2} *$";

        // One district, as an admin types it for a service area: "KT9", "SW19", "SW1A".
        public const string DistrictPattern = @"[A-Za-z]{1,2}[0-9]{1,2}[A-Za-z]?";

        // A service area's list of them: "KT9" or "KT5, KT6, KT7".
        public const string DistrictListPattern = @"^\s*" + DistrictPattern + @"(?:\s*,\s*" + DistrictPattern + @")*\s*$";

        private static readonly Regex PostcodeRegex = new Regex(Pattern, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

        private static readonly Regex DistrictRegex = new Regex("^" + DistrictPattern + "$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

        public static bool IsValid(string postcode)
        {
            return !string.IsNullOrWhiteSpace(postcode) && PostcodeRegex.IsMatch(postcode);
        }

        // "kt92qn" and " KT9  2QN " both become "KT9 2QN". Null for what is not a postcode.
        public static string Normalize(string postcode)
        {
            if (!IsValid(postcode))
            {
                return null;
            }

            var compact = Compact(postcode);
            return compact.Substring(0, compact.Length - 3) + " " + compact.Substring(compact.Length - 3);
        }

        // The district: "KT9" for "KT9 2QN". The inward code is always the last three characters.
        public static string GetOutwardCode(string postcode)
        {
            if (!IsValid(postcode))
            {
                return null;
            }

            var compact = Compact(postcode);
            return compact.Substring(0, compact.Length - 3);
        }

        // "kt5,  KT6 ,KT7" becomes KT5, KT6, KT7. Anything that is not a district is left out.
        public static IReadOnlyList<string> ParseDistricts(string districts)
        {
            if (string.IsNullOrWhiteSpace(districts))
            {
                return Array.Empty<string>();
            }

            return districts
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(d => Compact(d))
                .Where(d => DistrictRegex.IsMatch(d))
                .Distinct()
                .ToList();
        }

        // A few central London districts are split by a letter, "SW1A" inside "SW1". A postcode
        // in "SW1A" is in a listed "SW1"; a listed "SW1A" covers only itself.
        public static bool IsInDistricts(string postcode, IEnumerable<string> districts)
        {
            var outward = GetOutwardCode(postcode);
            if (outward == null || districts == null)
            {
                return false;
            }

            var withoutLetter = char.IsLetter(outward[outward.Length - 1])
                ? outward.Substring(0, outward.Length - 1)
                : outward;

            return districts.Any(d =>
                string.Equals(d, outward, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(d, withoutLetter, StringComparison.OrdinalIgnoreCase));
        }

        private static string Compact(string value)
        {
            return new string(value.Where(c => !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant();
        }
    }
}
