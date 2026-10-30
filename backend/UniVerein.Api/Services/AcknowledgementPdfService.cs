using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using UniVerein.DAL.Entities;

namespace UniVerein.Api.Services;

public class AcknowledgementPdfService
{
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");

    static AcknowledgementPdfService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] Render(PaymentAcknowledgementEntity acknowledgement)
    {
        string amount = acknowledgement.Amount.ToString("C", German);
        string paidAt = acknowledgement.PaidAt.UtcDateTime.ToString("dd.MM.yyyy", German);
        string issuedAt = acknowledgement.CreatedAt.UtcDateTime.ToString("dd.MM.yyyy", German);

        return Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(50);
                page.DefaultTextStyle(x => x.FontSize(11));

                page.Header().Column(column =>
                {
                    column.Item().Text(acknowledgement.IssuerName).FontSize(16).SemiBold();
                    if (!string.IsNullOrWhiteSpace(acknowledgement.IssuerAddress))
                        column.Item().Text(acknowledgement.IssuerAddress).FontColor(Colors.Grey.Darken2);
                });

                page.Content().PaddingTop(40).Column(column =>
                {
                    column.Spacing(20);
                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Text("Quittung").FontSize(26).Bold();
                        row.AutoItem().AlignBottom().Text($"Nr. {acknowledgement.Number}").FontSize(12);
                    });

                    column.Item().Border(1).BorderColor(Colors.Grey.Lighten1).Padding(15).Column(box =>
                    {
                        box.Spacing(10);
                        box.Item().Text(text =>
                        {
                            text.Span("Betrag: ").SemiBold();
                            text.Span(amount).FontSize(14).Bold();
                        });
                        Field(box, "Erhalten von", acknowledgement.PayerName);
                        if (!string.IsNullOrWhiteSpace(acknowledgement.PayerAddress))
                            Field(box, "Anschrift", acknowledgement.PayerAddress);
                        Field(box, "Verwendungszweck", acknowledgement.Purpose);
                        Field(box, "Zahlungsdatum", paidAt);
                    });

                    column.Item().Text(
                        $"Der oben genannte Betrag von {amount} wurde am {paidAt} dankend erhalten.");

                    column.Item().PaddingTop(60).Row(row =>
                    {
                        row.RelativeItem().Column(sign =>
                        {
                            sign.Item().Text(issuedAt);
                            sign.Item().PaddingTop(2).LineHorizontal(0.75f);
                            sign.Item().Text("Datum").FontSize(9).FontColor(Colors.Grey.Darken1);
                        });
                        row.ConstantItem(40);
                        row.RelativeItem().Column(sign =>
                        {
                            sign.Item().Height(14);
                            sign.Item().PaddingTop(2).LineHorizontal(0.75f);
                            sign.Item().Text("Unterschrift").FontSize(9).FontColor(Colors.Grey.Darken1);
                        });
                    });
                });

                page.Footer().Text("Diese Quittung ist keine Zuwendungsbestätigung im Sinne des § 10b EStG.")
                    .FontSize(8).FontColor(Colors.Grey.Darken1);
            });
        }).GeneratePdf();
    }

    private static void Field(ColumnDescriptor column, string label, string value)
    {
        column.Item().Text(text =>
        {
            text.Span($"{label}: ").SemiBold();
            text.Span(value);
        });
    }
}
