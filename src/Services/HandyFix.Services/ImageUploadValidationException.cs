namespace HandyFix.Services
{
    using System;

    // The photos a visitor attached, or the picture an admin chose for a service, are not ones the
    // site takes: too many, too large, or not a picture. Its message is written for that person
    // and is shown on the form. Any other failure while uploading is storage being out of reach
    // (or, for a service picture, the picture library failing), which is not theirs to fix and
    // must not be shown to them.
    public class ImageUploadValidationException : InvalidOperationException
    {
        public ImageUploadValidationException(string message)
            : base(message)
        {
        }
    }
}
