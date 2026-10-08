namespace HandyFix.Web.ViewModels.Validation
{
    using System;
    using System.ComponentModel.DataAnnotations;

    // A whole UK postcode. A RegularExpressionAttribute, like the other rules in this folder, so
    // ASP.NET Core writes it into the page as a pattern and the browser checks it while the
    // visitor types, with no script of its own.
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class UkPostcodeAttribute : RegularExpressionAttribute
    {
        public UkPostcodeAttribute()
            : base(UkPostcode.Pattern)
        {
            this.ErrorMessage = "Please enter a full UK postcode, for example KT9 2QN.";
        }
    }
}
