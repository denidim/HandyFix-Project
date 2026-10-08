namespace HandyFix.Services.Data.Common
{
    using System.Linq;
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

        // For the address of a tel: link: the number without the spaces it is written with, which
        // a link's address may not hold. The number shown beside the link keeps them.
        public static string PhoneLink(string phone)
        {
            return Encode(string.Concat((phone ?? string.Empty).Where(c => !char.IsWhiteSpace(c))));
        }
    }
}
