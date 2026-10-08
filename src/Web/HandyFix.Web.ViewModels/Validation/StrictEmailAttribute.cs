namespace HandyFix.Web.ViewModels.Validation
{
    using System;
    using System.ComponentModel.DataAnnotations;

    // An email address with a domain that ends in a dot and letters. [EmailAddress] only asks for
    // an "@" with something either side, so "john@gmail" passed and the reply never arrived.
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class StrictEmailAttribute : RegularExpressionAttribute
    {
        public const string EmailPattern = @"^[^@\s]+@[^@\s]+\.[A-Za-z]{2,}$";

        public StrictEmailAttribute()
            : base(EmailPattern)
        {
            this.ErrorMessage = "Please enter a full email address, for example name@example.com.";
        }
    }
}
