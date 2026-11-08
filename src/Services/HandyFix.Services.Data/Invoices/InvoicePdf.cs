namespace HandyFix.Services.Data.Invoices
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Reflection;

    using HandyFix.Common;
    using HandyFix.Web.ViewModels.Administration.Invoices;

    using QuestPDF;
    using QuestPDF.Drawing;
    using QuestPDF.Fluent;
    using QuestPDF.Helpers;
    using QuestPDF.Infrastructure;

    // The invoice and the month's list as PDF files, drawn with QuestPDF. The fonts are the
    // site's own, Outfit, carried inside this assembly: the servers' containers have no fonts at
    // all, and a PDF drawn with whatever a machine has would look different on each one. The
    // emblem is the tab icon's, read from the site's own file at build time.
    public static class InvoicePdf
    {
        private const string FontName = "Outfit";
        private const string LogoResource = "HandyFix.Invoices.Logo.svg";

        // The site's colours (wwwroot/css): its navy, its blue, and the greys of its admin pages.
        private const string Navy = "#0f172a";
        private const string Blue = "#006591";
        private const string Muted = "#64748b";
        private const string Rule = "#e2e8f0";
        private const string Panel = "#f1f5f9";
        private const string Green = "#15803d";
        private const string GreenPanel = "#dcfce7";
        private const string White = "#ffffff";

        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        private static readonly string Logo;

        // A constructor and not a field set where it is declared: such a field is filled only
        // when it is first read, which is after QuestPDF has asked for its licence.
        static InvoicePdf()
        {
            Logo = Setup();
        }

        public static byte[] ForInvoice(InvoiceViewModel invoice, string paymentDetails, string paymentUrl = null)
        {
            return InvoiceDocument(invoice, paymentDetails, paymentUrl).GeneratePdf();
        }

        public static byte[] ForMonth(IReadOnlyList<InvoiceViewModel> invoices, int year, int month, DateTime todayUk)
        {
            return MonthDocument(invoices, year, month, todayUk).GeneratePdf();
        }

        // The two documents before they are made into PDF files, for a test that draws them as
        // pictures to look at.
        public static IDocument InvoiceDocument(InvoiceViewModel invoice, string paymentDetails, string paymentUrl = null)
        {
            return Document
                .Create(document => document.Page(page =>
                {
                    Page(page, PageSizes.A4);
                    page.Header().Element(container => InvoiceHeader(container, invoice));
                    page.Content().PaddingTop(22).Element(container => InvoiceBody(container, invoice, paymentDetails, paymentUrl));
                    page.Footer().Element(CompanyFooter);
                }))
                .WithMetadata(new DocumentMetadata { Title = $"Invoice {invoice.NumberText}", Author = GlobalConstants.SystemName });
        }

        public static IDocument MonthDocument(IReadOnlyList<InvoiceViewModel> invoices, int year, int month, DateTime todayUk)
        {
            var title = "Invoices, " + new DateTime(year, month, 1).ToString("MMMM yyyy", Invariant);

            return Document
                .Create(document => document.Page(page =>
                {
                    Page(page, PageSizes.A4.Landscape());
                    page.Header().Row(row =>
                    {
                        row.ConstantItem(34).Height(34).Svg(Logo);
                        row.ConstantItem(10);
                        row.RelativeItem().AlignMiddle().Column(column =>
                        {
                            column.Item().Text(title).FontSize(15).Bold();
                            column.Item().Text($"{GlobalConstants.SystemName}, a trading name of {GlobalConstants.CompanyLegalName}").FontSize(9).FontColor(Muted);
                        });
                        row.AutoItem().AlignMiddle().Text("Made on " + Long(todayUk)).FontSize(9).FontColor(Muted);
                    });
                    page.Content().PaddingTop(16).Element(container => MonthTable(container, invoices, todayUk));
                    page.Footer().Element(CompanyFooter);
                }))
                .WithMetadata(new DocumentMetadata { Title = title, Author = GlobalConstants.SystemName });
        }

        private static void InvoiceHeader(IContainer container, InvoiceViewModel invoice)
        {
            container.Row(row =>
            {
                row.ConstantItem(56).Height(56).Svg(Logo);
                row.ConstantItem(12);
                row.RelativeItem().AlignMiddle().Column(column =>
                {
                    column.Item().Text(GlobalConstants.SystemName).FontSize(17).Bold();
                    column.Item().Text($"A trading name of {GlobalConstants.CompanyLegalName}").FontSize(9).FontColor(Muted);
                });
                row.ConstantItem(170).AlignMiddle().Column(column =>
                {
                    column.Item().AlignRight().Text("INVOICE").FontSize(24).Bold();
                    column.Item().AlignRight().Text(invoice.NumberText).FontSize(12).SemiBold().FontColor(Blue);
                });
            });
        }

        private static void InvoiceBody(IContainer container, InvoiceViewModel invoice, string paymentDetails, string paymentUrl)
        {
            container.Column(column =>
            {
                column.Spacing(20);

                // The dates and where the money stands, in one strip.
                column.Item().Background(Panel).Padding(12).Row(row =>
                {
                    Fact(row.RelativeItem(), "Invoice date", Long(invoice.InvoicedOn), Navy);
                    Fact(row.RelativeItem(), "Job reference", "#" + invoice.JobReference, Navy);
                    Fact(row.RelativeItem(), "Date of work", invoice.WorkDate.HasValue ? Long(invoice.WorkDate.Value) : "-", Navy);
                    if (invoice.IsPaid)
                    {
                        Fact(row.RelativeItem(), "Status", "PAID", Green);
                    }
                    else
                    {
                        Fact(row.RelativeItem(), "Due date", Long(invoice.DueOn), Navy);
                        Fact(row.RelativeItem(), "Terms", InvoiceTerms.PaymentTerms, Navy);
                    }
                });

                column.Item().Row(row =>
                {
                    row.RelativeItem().Column(party =>
                    {
                        Label(party, "Bill to");
                        party.Item().Text(invoice.CustomerName).FontSize(11).SemiBold();
                        Lines(party, invoice.Address, invoice.Email, invoice.PhoneNumber);
                    });
                    row.ConstantItem(24);
                    row.RelativeItem().Column(party =>
                    {
                        Label(party, "From");
                        party.Item().Text(GlobalConstants.SystemName).FontSize(11).SemiBold();
                        Lines(party, GlobalConstants.CompanyLegalName, GlobalConstants.CompanyAddress, GlobalConstants.BusinessPhone, GlobalConstants.BusinessEmail);
                    });
                });

                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn();
                        columns.ConstantColumn(110);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Element(HeadCell).Text("Description");
                        header.Cell().Element(HeadCell).AlignRight().Text("Amount");
                    });

                    table.Cell().Element(BodyCell).Column(work =>
                    {
                        work.Item().Text(string.IsNullOrWhiteSpace(invoice.Services) ? "Work carried out" : invoice.Services).SemiBold();
                        if (!string.IsNullOrWhiteSpace(invoice.WorkDone))
                        {
                            work.Item().PaddingTop(4).Text(invoice.WorkDone).FontColor(Muted).LineHeight(1.35f);
                        }
                    });
                    table.Cell().Element(BodyCell).AlignRight().Text(Pounds(invoice.Total)).SemiBold();
                });

                // What was paid towards it, and what is left.
                column.Item().AlignRight().Width(310).Column(totals =>
                {
                    TotalRow(totals, "Total", Pounds(invoice.Total), true);
                    foreach (InvoicePaymentLine payment in invoice.Payments)
                    {
                        TotalRow(totals, $"{payment.Description}, {Short(payment.PaidOn)}", "-" + Pounds(payment.Amount), false);
                    }

                    totals.Item().PaddingTop(8).Background(invoice.IsPaid ? GreenPanel : Navy).Padding(10).Row(row =>
                    {
                        var colour = invoice.IsPaid ? Green : White;
                        row.RelativeItem().Text(invoice.IsPaid ? "Paid in full" : "Balance due").FontSize(12).Bold().FontColor(colour);
                        row.AutoItem().Text(Pounds(invoice.Balance)).FontSize(12).Bold().FontColor(colour);
                    });
                });

                if (!invoice.IsPaid)
                {
                    column.Item().Border(1).BorderColor(Rule).Padding(12).Column(pay =>
                    {
                        pay.Item().Text($"Please pay {Pounds(invoice.Balance)} on completion ({Long(invoice.DueOn)}). Terms: {InvoiceTerms.PaymentTerms}.").SemiBold();
                        if (!string.IsNullOrWhiteSpace(paymentUrl))
                        {
                            pay.Item().PaddingTop(4).Text(text =>
                            {
                                text.Span("Pay online (Card, Apple Pay, Google Pay): ").SemiBold();
                                text.Hyperlink(paymentUrl, paymentUrl).FontColor(Blue).Underline();
                            });
                        }

                        pay.Item().PaddingTop(4).Text(paymentDetails).LineHeight(1.35f);
                        pay.Item().PaddingTop(4).Text($"Please give {invoice.NumberText} as the reference.").FontColor(Muted);
                    });
                }

                column.Item().Text($"Thank you for choosing {GlobalConstants.SystemName}.").FontColor(Muted);
            });
        }

        private static void MonthTable(IContainer container, IReadOnlyList<InvoiceViewModel> invoices, DateTime todayUk)
        {
            if (invoices.Count == 0)
            {
                container.Text("No invoices were made in this month.").FontColor(Muted);
                return;
            }

            container.Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(62);
                    columns.ConstantColumn(66);
                    columns.RelativeColumn(2);
                    columns.ConstantColumn(76);
                    foreach (var unused in Enumerable.Range(0, 6))
                    {
                        columns.RelativeColumn();
                    }

                    columns.ConstantColumn(66);
                });

                table.Header(header =>
                {
                    foreach (var name in new[] { "Invoice", "Date", "Customer", "Job" })
                    {
                        header.Cell().Element(HeadCell).Text(name);
                    }

                    foreach (var name in new[] { "Total", "Deposit", "Card", "Cash", "Transfer", "Balance" })
                    {
                        header.Cell().Element(HeadCell).AlignRight().Text(name);
                    }

                    header.Cell().Element(HeadCell).AlignRight().Text("Status");
                });

                foreach (InvoiceViewModel invoice in invoices)
                {
                    table.Cell().Element(BodyCell).Text(invoice.NumberText).SemiBold();
                    table.Cell().Element(BodyCell).Text(Short(invoice.InvoicedOn));
                    table.Cell().Element(BodyCell).Text(invoice.CustomerName);
                    table.Cell().Element(BodyCell).Text("#" + invoice.JobReference);
                    foreach (var amount in new[] { invoice.Total, invoice.PaidDeposit, invoice.PaidByCard, invoice.PaidInCash, invoice.PaidByBankTransfer, invoice.Balance })
                    {
                        table.Cell().Element(BodyCell).AlignRight().Text(Pounds(amount));
                    }

                    var status = invoice.StatusOn(todayUk);
                    table.Cell().Element(BodyCell).AlignRight().Text(status).SemiBold().FontColor(invoice.IsPaid ? Green : invoice.IsOverdueOn(todayUk) ? "#b91c1c" : Navy);
                }

                // The month's sums, under the money columns.
                table.Cell().ColumnSpan(4).Element(SumCell).Text($"{invoices.Count} invoice{(invoices.Count == 1 ? string.Empty : "s")}").Bold();
                foreach (var sum in new[]
                {
                    invoices.Sum(x => x.Total),
                    invoices.Sum(x => x.PaidDeposit),
                    invoices.Sum(x => x.PaidByCard),
                    invoices.Sum(x => x.PaidInCash),
                    invoices.Sum(x => x.PaidByBankTransfer),
                    invoices.Sum(x => x.Balance),
                })
                {
                    table.Cell().Element(SumCell).AlignRight().Text(Pounds(sum)).Bold();
                }

                table.Cell().Element(SumCell);
            });
        }

        // The company's details every page carries: the law asks a limited company to name
        // itself, its number and its registered office on its invoices.
        private static void CompanyFooter(IContainer container)
        {
            container.BorderTop(1).BorderColor(Rule).PaddingTop(8).Column(column =>
            {
                column.Item().Text($"{GlobalConstants.SystemName} is a trading name of {GlobalConstants.CompanyLegalName}, registered in England and Wales, company number {GlobalConstants.CompanyNumber}. Registered office: {GlobalConstants.CompanyAddress}.")
                    .FontSize(8).FontColor(Muted).AlignCenter();
                column.Item().Text(text =>
                {
                    text.AlignCenter();
                    text.DefaultTextStyle(style => style.FontSize(8).FontColor(Muted));
                    text.Span("Not registered for VAT: no VAT is charged.   Page ");
                    text.CurrentPageNumber();
                    text.Span(" of ");
                    text.TotalPages();
                });
            });
        }

        private static void Page(PageDescriptor page, PageSize size)
        {
            page.Size(size);
            page.Margin(40);
            page.PageColor(White);
            page.DefaultTextStyle(style => style.FontFamily(FontName).FontSize(10).FontColor(Navy));
        }

        private static void Fact(IContainer container, string label, string value, string colour)
        {
            container.Column(column =>
            {
                column.Item().Text(label.ToUpperInvariant()).FontSize(7.5f).SemiBold().FontColor(Muted);
                column.Item().PaddingTop(2).Text(value).FontSize(11).Bold().FontColor(colour);
            });
        }

        private static void Label(ColumnDescriptor column, string text)
        {
            column.Item().PaddingBottom(4).Text(text.ToUpperInvariant()).FontSize(7.5f).SemiBold().FontColor(Muted);
        }

        // Each line that has something in it; a job written in may have no address or email.
        private static void Lines(ColumnDescriptor column, params string[] lines)
        {
            foreach (var line in lines.Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                column.Item().PaddingTop(1).Text(line).FontColor(Muted);
            }
        }

        private static void TotalRow(ColumnDescriptor column, string label, string amount, bool strong)
        {
            column.Item().BorderBottom(1).BorderColor(Rule).PaddingVertical(5).Row(row =>
            {
                TextBlockDescriptor left = row.RelativeItem().Text(label);
                TextBlockDescriptor right = row.AutoItem().PaddingLeft(12).Text(amount);
                if (strong)
                {
                    left.SemiBold();
                    right.SemiBold();
                }
                else
                {
                    left.FontColor(Muted);
                }
            });
        }

        private static IContainer HeadCell(IContainer container)
        {
            return container.BorderBottom(1.5f).BorderColor(Navy).PaddingVertical(6).PaddingHorizontal(3).DefaultTextStyle(style => style.FontSize(8.5f).SemiBold().FontColor(Muted));
        }

        private static IContainer BodyCell(IContainer container)
        {
            return container.BorderBottom(1).BorderColor(Rule).PaddingVertical(7).PaddingHorizontal(3);
        }

        private static IContainer SumCell(IContainer container)
        {
            return container.Background(Panel).PaddingVertical(7).PaddingHorizontal(3);
        }

        private static string Pounds(decimal amount)
        {
            return "£" + amount.ToString("#,0.00", Invariant);
        }

        private static string Long(DateTime date)
        {
            return date.ToString("d MMMM yyyy", Invariant);
        }

        private static string Short(DateTime date)
        {
            return date.ToString("d MMM yyyy", Invariant);
        }

        // Runs once, before the first PDF. The Community licence is QuestPDF's free one for a
        // business with less than $1M a year. Fonts come only from this assembly, so a machine's
        // own fonts never change the look; a character Outfit lacks is left blank rather than
        // failing the whole PDF.
        private static string Setup()
        {
            Settings.License = LicenseType.Community;
            Settings.UseSystemFonts = false;
            Settings.ThrowOnMissingTextGlyphs = false;

            Assembly assembly = typeof(InvoicePdf).Assembly;
            foreach (var name in assembly.GetManifestResourceNames().Where(x => x.EndsWith(".ttf", StringComparison.Ordinal)))
            {
                using Stream font = assembly.GetManifestResourceStream(name);
                FontManager.RegisterFontFromStream(font);
            }

            using Stream logo = assembly.GetManifestResourceStream(LogoResource);
            using var reader = new StreamReader(logo);
            return reader.ReadToEnd();
        }
    }
}
