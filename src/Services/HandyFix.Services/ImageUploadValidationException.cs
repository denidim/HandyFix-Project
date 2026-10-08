namespace HandyFix.Services
{
    using System;

    // The photos a visitor attached are not ones the site takes: too many, too large, or not a
    // picture. Its message is written for the visitor and is shown on the form. Any other failure
    // while uploading is storage being out of reach, which is not the visitor's to fix and must
    // not be shown to them.
    public class ImageUploadValidationException : InvalidOperationException
    {
        public ImageUploadValidationException(string message)
            : base(message)
        {
        }
    }
}
