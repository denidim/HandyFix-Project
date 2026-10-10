namespace HandyFix.Services.Data.Common
{
    using HandyFix.Common;

    // The look every email the site sends shares: the logo across the top, a coloured line under
    // it, the content, a footer. An email is put together from the pieces below and wrapped by
    // one of the three "For..." methods, so the look is changed here once and not in seven
    // places (PROJECT_STATE.md Section 3cl).
    //
    // Every argument is HTML, ready to go into the page. Anything a person typed has to pass
    // through EmailText first: nothing here encodes for the caller.
    //
    // An email program understands far less than a browser. The layout is tables with the styles
    // written on each cell, because several of them (Outlook above all) ignore a stylesheet and
    // most of what a div can do. The one stylesheet, in the head, is for phones, which do read it.
    public static class EmailLayout
    {
        // The logo is a picture on the live site, whichever copy of the site sends the email: an
        // email program fetches it from the internet, and staging sits behind a password. A
        // picture packed into the email itself is not shown by Gmail.
        public const string LogoUrl = GlobalConstants.SiteUrl + "/images/email/logo-header.png";

        private const string Navy = "#13284b";
        private const string Teal = "#14697e";
        private const string Gold = "#b8975a";
        private const string WhatsAppGreen = "#25D366";

        // To a customer, or to anyone outside the company: the full footer, with how to reach
        // the business and the company behind it.
        public static string ForCustomer(string content)
        {
            return Page(content, 300, Gold, CustomerFooter());
        }

        // A notice to the company's own inbox.
        public static string ForCompany(string content)
        {
            return Page(content, 280, Teal, OneLineFooter("Automated Internal System Notification"));
        }

        // About the admin account: the password reset link.
        public static string ForAccountSecurity(string content)
        {
            return Page(content, 280, Navy, OneLineFooter("Automated Account Security Service"));
        }

        // The small rounded label at the top, saying in three words what the email is.
        public static string Badge(EmailColour colour, string html)
        {
            (string background, string text, string border) = colour switch
            {
                EmailColour.Blue => ("#eff6ff", "#1d4ed8", "#bfdbfe"),
                EmailColour.Red => ("#fef2f2", "#b91c1c", "#fecaca"),
                EmailColour.Amber => ("#fffbeb", "#b45309", "#fde68a"),
                _ => ("#ecfdf5", "#047857", "#a7f3d0"),
            };

            var piece = $@"<div style=""display: inline-block; background-color: {background}; color: {text}; border: 1px solid {border}; padding: 6px 14px; border-radius: 20px; font-size: 13px; font-weight: 700; text-transform: uppercase; letter-spacing: 0.5px;"">{html}</div>";

            return Block(14, piece);
        }

        public static string Heading(string html)
        {
            return Block(12, html, $"font-family: Georgia, serif; font-size: 24px; font-weight: bold; color: {Navy};");
        }

        // The first paragraph, under the heading.
        public static string Lead(string html)
        {
            return Block(25, html, "font-size: 15px; line-height: 1.6; color: #475569;");
        }

        // A paragraph further down.
        public static string Text(string html)
        {
            return Block(25, html, "font-size: 14px; line-height: 1.6; color: #475569;");
        }

        // Grey small print at the end.
        public static string SmallPrint(string html)
        {
            return Block(10, html, "font-size: 13px; line-height: 1.5; color: #94a3b8;");
        }

        // An address written out in full, for when a button does not work. It has no spaces to
        // wrap at and would push the email wider than a phone, so it may break anywhere.
        public static string LinkInFull(string url)
        {
            return $@"<a href=""{url}"" style=""color: {Teal}; word-break: break-all;"">{url}</a>";
        }

        // The grey card of labelled lines: the reference, the service, the time. Its rows come
        // from the Row methods and Divider below.
        public static string Details(params string[] rows)
        {
            var piece = $@"<table border=""0"" cellpadding=""0"" cellspacing=""0"" width=""100%"" style=""background-color: #f8fafc; border: 1px solid #e2e8f0; border-radius: 8px;"">
<tr><td style=""padding: 20px 20px 10px 20px;"">
<table border=""0"" cellpadding=""0"" cellspacing=""0"" width=""100%"" style=""font-size: 14px; line-height: 1.5;"">
{string.Concat(rows)}
</table>
</td></tr>
</table>";

            return Block(25, piece);
        }

        public static string Row(string label, string html)
        {
            return DetailsRow(label, html, "color: #0f172a; font-weight: 700;");
        }

        // For a longer value, such as an address: not bold.
        public static string PlainRow(string label, string html)
        {
            return DetailsRow(label, html, "color: #0f172a;");
        }

        // The booking's reference, written the way the admin's job list shows it.
        public static string ReferenceRow(string reference)
        {
            return DetailsRow("Booking Reference", "#" + reference, "color: #0f172a; font-weight: 700; font-family: monospace; font-size: 15px;");
        }

        public static string TimeRow(string label, string html)
        {
            return DetailsRow(label, html, $"color: {Teal}; font-weight: 700;");
        }

        public static string MoneyRow(string label, string html)
        {
            return DetailsRow(label, html, "color: #16a34a; font-weight: 700;");
        }

        // A dashed line across the card, before the money.
        public static string Divider()
        {
            return @"<tr><td colspan=""2"" style=""border-top: 1px dashed #cbd5e1; padding-top: 10px; font-size: 0; line-height: 0;"">&nbsp;</td></tr>";
        }

        // One thing made to stand out, with a teal edge: the technician.
        public static string Spotlight(string label, string titleHtml, string lineHtml = null)
        {
            var line = string.IsNullOrEmpty(lineHtml)
                ? string.Empty
                : $@"<div style=""font-size: 14px; color: #334155; margin-top: 6px;"">{lineHtml}</div>";

            var piece = $@"<table border=""0"" cellpadding=""0"" cellspacing=""0"" width=""100%"" style=""background-color: #f8fafc; border: 1px solid #e2e8f0; border-left: 4px solid {Teal}; border-radius: 6px;"">
<tr><td style=""padding: 18px 20px;"">
<div style=""font-size: 13px; color: #64748b; font-weight: 600; text-transform: uppercase; letter-spacing: 0.5px; margin-bottom: 4px;"">{label}</div>
<div style=""font-size: 18px; font-weight: 700; color: {Navy};"">{titleHtml}</div>
{line}
</td></tr>
</table>";

            return Block(25, piece);
        }

        // A grey box with a gold edge, for something the reader should not miss.
        public static string Note(string titleHtml, string html)
        {
            var title = string.IsNullOrEmpty(titleHtml)
                ? string.Empty
                : $@"<div style=""font-weight: 700; color: {Navy}; margin-bottom: 4px;"">{titleHtml}</div>";

            var piece = $@"<table border=""0"" cellpadding=""0"" cellspacing=""0"" width=""100%"" style=""background-color: #f8fafc; border: 1px solid #e2e8f0; border-left: 4px solid {Gold}; border-radius: 6px;"">
<tr><td style=""padding: 18px 20px; font-size: 14px; line-height: 1.5; color: #475569;"">
{title}{html}
</td></tr>
</table>";

            return Block(25, piece);
        }

        // A blue box telling the company what to do next.
        public static string Advice(string html)
        {
            var piece = $@"<table border=""0"" cellpadding=""0"" cellspacing=""0"" width=""100%"" style=""background-color: #eff6ff; border: 1px solid #bfdbfe; border-radius: 6px;"">
<tr><td style=""padding: 16px 20px; font-size: 13px; line-height: 1.6; color: #1e40af;"">{html}</td></tr>
</table>";

            return Block(25, piece);
        }

        // What somebody wrote to the company, set apart from the site's own words.
        public static string Quote(string title, string html)
        {
            var box = $@"<table border=""0"" cellpadding=""0"" cellspacing=""0"" width=""100%"">
<tr><td style=""background-color: #ffffff; border: 1px solid #cbd5e1; border-left: 4px solid {Navy}; padding: 18px; border-radius: 6px; font-size: 14px; line-height: 1.6; color: #334155;"">{html}</td></tr>
</table>";

            return Block(8, title, $"font-size: 14px; font-weight: 700; color: {Navy};") + Block(20, box);
        }

        public static string Button(string label, string url)
        {
            return ButtonIn(Navy, label, url);
        }

        public static string WhatsAppButton(string label)
        {
            return ButtonIn(WhatsAppGreen, "&#128172; " + label, GlobalConstants.BusinessWhatsAppUrl);
        }

        // A link inside a line of text, in the site's teal.
        public static string Link(string url, string html)
        {
            return $@"<a href=""{url}"" style=""color: {Teal}; font-weight: 600; text-decoration: none;"">{html}</a>";
        }

        // The colour is on the cell and not only on the link, because Outlook paints a link's
        // background behind its words alone and leaves the padding white.
        private static string ButtonIn(string colour, string label, string url)
        {
            var button = $@"<table border=""0"" cellpadding=""0"" cellspacing=""0"" align=""center"">
<tr><td align=""center"" bgcolor=""{colour}"" style=""border-radius: 8px;"">
<a class=""button-link"" href=""{url}"" target=""_blank"" style=""display: inline-block; padding: 13px 26px; color: #ffffff !important; font-size: 15px; font-weight: bold; text-decoration: none; border-radius: 8px;"">{label}</a>
</td></tr>
</table>";

            return Block(20, button, align: "center");
        }

        private static string DetailsRow(string label, string html, string valueStyle)
        {
            return $@"<tr>
<td class=""data-row-label"" width=""40%"" valign=""top"" style=""padding-bottom: 10px; color: #64748b; font-weight: 600;"">{label}:</td>
<td class=""data-row-value"" width=""60%"" valign=""top"" style=""padding-bottom: 10px; {valueStyle}"">{html}</td>
</tr>
";
        }

        // One piece of the email with the space under it. The space is padding on a cell: a
        // margin is one more thing Outlook ignores.
        private static string Block(int spaceBelow, string html, string style = null, string align = "left")
        {
            return $@"<table border=""0"" cellpadding=""0"" cellspacing=""0"" width=""100%"">
<tr><td align=""{align}"" style=""padding-bottom: {spaceBelow}px; {style}"">{html}</td></tr>
</table>
";
        }

        private static string CustomerFooter()
        {
            return $@"<tr>
<td bgcolor=""#f8fafc"" style=""padding: 24px 30px; border-top: 1px solid #e2e8f0; text-align: center;"">
<table border=""0"" cellpadding=""0"" cellspacing=""0"" width=""100%"" style=""font-size: 12px; line-height: 1.6; color: #64748b;"">
<tr><td align=""center"" style=""padding-bottom: 10px;"">
<strong style=""color: {Navy}; font-size: 13px;"">{GlobalConstants.SystemName}</strong><br />
&#128222; Phone: <a href=""tel:{GlobalConstants.BusinessPhoneInternational}"" style=""color: {Navy}; font-weight: 700; text-decoration: none;"">{GlobalConstants.BusinessPhone}</a> &nbsp;|&nbsp;
&#9993;&#65039; <a href=""mailto:{GlobalConstants.BusinessEmail}"" style=""color: {Teal}; text-decoration: none;"">{GlobalConstants.BusinessEmail}</a>
</td></tr>
<tr><td align=""center"" style=""padding-bottom: 12px; border-bottom: 1px solid #e2e8f0;"">
Serving Chessington, Surbiton, Kingston, Sutton, Epsom &amp; South West London
</td></tr>
<tr><td align=""center"" style=""padding-top: 12px; font-size: 11px; color: #94a3b8;"">
{GlobalConstants.SystemName} is a trading name of <strong>{GlobalConstants.CompanyLegalName}</strong>, registered in England and Wales, company number {GlobalConstants.CompanyNumber}.<br />
Registered office: {GlobalConstants.CompanyAddress}.
</td></tr>
</table>
</td>
</tr>";
        }

        private static string OneLineFooter(string what)
        {
            return $@"<tr>
<td bgcolor=""#f8fafc"" align=""center"" style=""padding: 20px 30px; border-top: 1px solid #e2e8f0; text-align: center; font-size: 11px; color: #94a3b8;"">
{GlobalConstants.SystemName} &bull; {what}
</td>
</tr>";
        }

        private static string Page(string content, int logoWidth, string accent, string footer)
        {
            return $@"<!DOCTYPE html PUBLIC ""-//W3C//DTD XHTML 1.0 Transitional//EN"" ""http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd"">
<html xmlns=""http://www.w3.org/1999/xhtml"" lang=""en"">
<head>
<meta http-equiv=""Content-Type"" content=""text/html; charset=UTF-8"" />
<meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" />
<title>{GlobalConstants.SystemName}</title>
<style type=""text/css"">
body {{ margin: 0; padding: 0; min-width: 100%; width: 100% !important; height: 100% !important; background-color: #f1f5f9; -webkit-font-smoothing: antialiased; }}
table {{ border-spacing: 0; font-family: 'Segoe UI', -apple-system, BlinkMacSystemFont, Roboto, Helvetica, Arial, sans-serif; color: #1e293b; }}
td {{ padding: 0; }}
img {{ border: 0; }}
a {{ color: {Teal}; text-decoration: underline; }}
.button-link {{ text-decoration: none !important; }}
@media only screen and (max-width: 620px) {{
.email-container {{ width: 100% !important; }}
.content-cell {{ padding: 22px 16px !important; }}
.data-row-label {{ width: 100% !important; display: block !important; padding-bottom: 2px !important; }}
.data-row-value {{ width: 100% !important; display: block !important; padding-bottom: 12px !important; }}
}}
</style>
</head>
<body style=""margin: 0; padding: 0; background-color: #f1f5f9;"">
<table width=""100%"" border=""0"" cellpadding=""0"" cellspacing=""0"" bgcolor=""#f1f5f9"" style=""table-layout: fixed;"">
<tr>
<td align=""center"" style=""padding: 25px 10px 40px 10px;"">
<table class=""email-container"" width=""600"" border=""0"" cellpadding=""0"" cellspacing=""0"" style=""max-width: 600px; width: 100%; margin: 0 auto; background-color: #ffffff; border-radius: 12px; overflow: hidden; box-shadow: 0 4px 15px rgba(15, 23, 42, 0.06); border: 1px solid #e2e8f0;"">
<tr>
<td bgcolor=""#ffffff"" align=""center"" style=""padding: 26px 20px 20px 20px; border-bottom: 3px solid {Navy};"">
<a href=""{GlobalConstants.SiteUrl}"" target=""_blank"" style=""text-decoration: none;"">
<img src=""{LogoUrl}"" alt=""{GlobalConstants.SystemName}"" width=""{logoWidth}"" style=""display: block; width: {logoWidth}px; max-width: 85%; height: auto; margin: 0 auto; border: 0; font-family: Georgia, serif; font-size: 20px; font-weight: bold; color: {Navy};"" />
</a>
</td>
</tr>
<tr>
<td height=""4"" bgcolor=""{accent}"" style=""background-color: {accent}; font-size: 0; line-height: 0;"">&nbsp;</td>
</tr>
<tr>
<td class=""content-cell"" style=""padding: 35px 35px 25px 35px;"">
{content}
</td>
</tr>
{footer}
</table>
</td>
</tr>
</table>
</body>
</html>";
        }
    }
}
