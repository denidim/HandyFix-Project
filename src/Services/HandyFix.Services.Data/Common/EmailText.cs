namespace HandyFix.Services.Data.Common
{
    using System.Net;

    // Text typed into a form, made safe to put into an email's HTML. The emails are built as
    // HTML strings, so a name or an address written into one as it was typed could carry markup:
    // a link, an image, a whole fake paragraph, in an email that comes from the business.
    public static class EmailText
    {
        public static string Encode(string text)
        {
            return WebUtility.HtmlEncode(text ?? string.Empty);
        }

        // For a message of several lines: encoded, with its line breaks kept as line breaks.
        public static string EncodeLines(string text)
        {
            return Encode(text).Replace("\r\n", "\n").Replace("\n", "<br />");
        }
    }
}
