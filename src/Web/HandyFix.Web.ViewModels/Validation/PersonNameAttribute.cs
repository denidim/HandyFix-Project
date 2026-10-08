namespace HandyFix.Web.ViewModels.Validation
{
    using System;
    using System.ComponentModel.DataAnnotations;

    // A person's name: letters, with spaces, hyphens and apostrophes between them ("Mary-Jane
    // O'Neil"). Accented letters count. Digits, web addresses and symbols do not, which is what
    // most junk typed into a name box is made of.
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class PersonNameAttribute : RegularExpressionAttribute
    {
        // \u00C0-\u024F is the accented Latin letters. \u2019 is the curly apostrophe a phone
        // keyboard types. Both escapes are read by the regex engine, in .NET and in the browser.
        public const string NamePattern = @"^ *[A-Za-z\u00C0-\u024F][A-Za-z\u00C0-\u024F '\u2019-]*$";

        public PersonNameAttribute()
            : base(NamePattern)
        {
            this.ErrorMessage = "Please use letters only. Spaces, hyphens and apostrophes are fine.";
        }
    }
}
