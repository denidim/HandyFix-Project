namespace HandyFix.Web.ViewModels.Validation
{
    using System;
    using System.ComponentModel.DataAnnotations;

    // A UK mobile (07...) or landline (01, 02, 03...), written the UK way or with +44, with
    // spaces or hyphens anywhere between the digits. [Phone] accepted any run of digits, so a
    // mistyped number was only found out when someone tried to call it.
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class UkPhoneAttribute : RegularExpressionAttribute
    {
        // After the leading 0 or +44: a mobile is 7 and nine more digits, a landline is 1, 2 or 3
        // and eight or nine more. "+44 (0)20..." and "0044..." are accepted too, as people write
        // them. [0-9], not \d: \d also matches other scripts' digits in .NET but not in a browser.
        public const string PhonePattern = @"^ *(?:(?:\+|00)44[ -]?(?:\(0\)[ -]?|0)?|0)(?:7(?:[ -]?[0-9]){9}|[123](?:[ -]?[0-9]){8,9}) *$";

        // The example mobile is from 07700 900000 to 900999, the range Ofcom keeps for made-up
        // numbers, so it is nobody's.
        public UkPhoneAttribute()
            : base(PhonePattern)
        {
            this.ErrorMessage = "Please enter a UK phone number, for example 07700 900123 or 020 3951 5915.";
        }
    }
}
