using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using UniVerein.Api.Helper;
using UniVerein.Api.Models.Eur;
using UniVerein.DAL.Entities.Enums;

namespace UniVerein.Api.Services;

public class EurWorkbookWriter
{
    private const string MoneyFormat = "#,##0.00 \"€\"";
    private const string PercentFormat = "0.0%";
    private const string DateFormat = "dd.MM.yyyy";

    private static readonly XLColor TitleColor = XLColor.FromHtml("#1F3864");
    private static readonly XLColor HeaderFill = XLColor.FromHtml("#D9E2F3");
    private static readonly XLColor InputFill = XLColor.FromHtml("#FFF2CC");
    private static readonly XLColor OkFont = XLColor.FromHtml("#2E7D32");
    private static readonly XLColor FailFont = XLColor.FromHtml("#C62828");

    public byte[] Write(EurReport report)
    {
        using XLWorkbook workbook = new();
        workbook.Properties.Title = $"Einnahmen-Überschuss-Rechnung {report.Year}";

        WriteEurSheet(workbook.AddWorksheet("EÜR"), report);
        WriteMasterDataSheet(workbook.AddWorksheet("Stammdaten"), report);
        WriteJournalSheet(workbook.AddWorksheet("Journal"), report);
        WriteContributionProofSheet(workbook.AddWorksheet("Beitragsnachweis"), report);
        WriteNotesSheet(workbook.AddWorksheet("Hinweise"));

        using MemoryStream stream = new();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void WriteEurSheet(IXLWorksheet ws, EurReport report)
    {
        SetWidths(ws, 42, 16, 10, 3, 72, 16, 16, 28);

        Label(ws, 1, 1, "Einnahmen-Überschuss-Rechnung").Style.Font.SetBold().Font.SetFontSize(16).Font.SetFontColor(TitleColor);
        Text(ws, 2, 1, HeaderLine(report));
        Label(ws, 3, 1, $"Geschäftsjahr {report.Year}  (01.01.{report.Year} – 31.12.{report.Year})");

        int row = 5;
        foreach (EurAreaBlock block in report.Areas)
        {
            Header(ws, row, 1, 3, EurLabels.AreaName(block.Area), "Betrag", "Anteil");
            row++;
            row = WriteSide(ws, row, "Einnahmen", block.Income, block.TotalIncome, "Summe Einnahmen");
            row = WriteSide(ws, row, "Ausgaben", block.Expenses, block.TotalExpenses, "Summe Ausgaben");
            Label(ws, row, 1, "Ergebnis (Überschuss / Fehlbetrag)");
            Money(ws, row, 2, block.Result);
            ws.Range(row, 1, row, 3).Style.Font.SetBold().Border.SetTopBorder(XLBorderStyleValues.Double);
            row += 2;
        }

        WriteOverview(ws, report);
        WriteTaxChecks(ws, report);
        WriteBalanceReconciliation(ws, report);
        WriteSignatures(ws, report);
    }

    private static int WriteSide(IXLWorksheet ws, int row, string title, List<EurCategoryRow> rows, decimal total, string totalLabel)
    {
        Label(ws, row, 1, title).Style.Font.SetBold();
        row++;
        foreach (EurCategoryRow categoryRow in rows)
        {
            Text(ws, row, 1, categoryRow.Name);
            Money(ws, row, 2, categoryRow.Amount);
            Percent(ws, row, 3, total == 0 ? 0 : categoryRow.Amount / total);
            row++;
        }

        Label(ws, row, 1, totalLabel);
        Money(ws, row, 2, total);
        ws.Range(row, 1, row, 3).Style.Font.SetBold().Border.SetTopBorder(XLBorderStyleValues.Thin);
        return row + 1;
    }

    private static void WriteOverview(IXLWorksheet ws, EurReport report)
    {
        Label(ws, 5, 5, "Gesamtübersicht nach Bereichen").Style.Font.SetBold().Font.SetFontSize(12);
        Header(ws, 6, 5, 4, "Bereich", "Einnahmen", "Ausgaben", "Ergebnis");

        int row = 7;
        foreach (EurAreaBlock block in report.Areas)
        {
            Label(ws, row, 5, EurLabels.AreaName(block.Area));
            Money(ws, row, 6, block.TotalIncome);
            Money(ws, row, 7, block.TotalExpenses);
            Money(ws, row, 8, block.Result);
            row++;
        }

        Label(ws, row, 5, "Gesamtergebnis Verein");
        Money(ws, row, 6, report.TotalIncome);
        Money(ws, row, 7, report.TotalExpenses);
        Money(ws, row, 8, report.TotalIncome - report.TotalExpenses);
        ws.Range(row, 5, row, 8).Style.Font.SetBold().Border.SetTopBorder(XLBorderStyleValues.Double);
    }

    private static void WriteTaxChecks(IXLWorksheet ws, EurReport report)
    {
        Label(ws, 14, 5, $"Steuerliche Prüfungen {report.Year}").Style.Font.SetBold().Font.SetFontSize(12);
        Header(ws, 15, 5, 4, "Prüfung", "Wert", "Grenze", "Status");

        int row = 16;
        foreach (EurTaxCheck check in report.TaxChecks)
        {
            Label(ws, row, 5, check.Label);
            Money(ws, row, 6, check.Value);
            Money(ws, row, 7, check.Limit);
            Label(ws, row, 8, check.Status).Style.Font.SetFontColor(check.Ok ? OkFont : FailFont).Font.SetBold();
            row++;
        }

        Label(ws, row, 5, report.TaxLimitsNote).Style.Font.SetItalic();
    }

    private static void WriteBalanceReconciliation(IXLWorksheet ws, EurReport report)
    {
        Label(ws, 22, 5, "Abgleich Kasse / Bank").Style.Font.SetBold().Font.SetFontSize(12);
        Header(ws, 23, 5, 4, "Konto", "Bank", "Kasse", "Gesamt");

        Label(ws, 24, 5, "Anfangsbestand 01.01.");
        Label(ws, 25, 5, "+ Zuflüsse (inkl. Geldtransit)");
        Label(ws, 26, 5, "– Abflüsse (inkl. Geldtransit)");
        Label(ws, 27, 5, "Endbestand 31.12.").Style.Font.SetBold();
        Label(ws, 28, 5, "Kontrolle: Anfang + Überschuss = Ende");
        Label(ws, 29, 5, "Kontrolle: Geldtransit gleicht sich aus");
        Label(ws, 30, 5, "Kontrolle: Fehler im Journal");

        for (int row = 24; row <= 27; row++)
        {
            for (int column = 6; column <= 8; column++)
            {
                IXLCell cell = ws.Cell(row, column);
                cell.Style.Fill.SetBackgroundColor(InputFill).NumberFormat.SetFormat(MoneyFormat);
            }
        }

        ws.Cell(28, 8).Style.Fill.SetBackgroundColor(InputFill);
        ws.Cell(29, 8).Style.Fill.SetBackgroundColor(InputFill);

        bool journalOk = report.JournalErrors == 0;
        Label(ws, 30, 8, journalOk ? "✔ keine" : $"✖ {report.JournalErrors} Zeile(n)")
            .Style.Font.SetFontColor(journalOk ? OkFont : FailFont).Font.SetBold();
    }

    private static void WriteSignatures(IXLWorksheet ws, EurReport report)
    {
        Label(ws, 33, 5, "Ort, Datum, Unterschrift Kassenwart/in");
        Label(ws, 33, 7, "Kassenprüfer/in");
        ws.Range(33, 5, 33, 6).Style.Border.SetTopBorder(XLBorderStyleValues.Thin);
        ws.Range(33, 7, 33, 8).Style.Border.SetTopBorder(XLBorderStyleValues.Thin);

        if (!string.IsNullOrWhiteSpace(report.Treasurer))
            Text(ws, 34, 5, report.Treasurer).Style.Font.SetItalic();
    }

    private static void WriteMasterDataSheet(IXLWorksheet ws, EurReport report)
    {
        SetWidths(ws, 42, 38, 14);

        Label(ws, 1, 1, "Stammdaten & Kategorien").Style.Font.SetBold().Font.SetFontSize(16).Font.SetFontColor(TitleColor);
        Label(ws, 2, 1, "Gelb hinterlegte Zellen sind leer und können von Hand ausgefüllt werden.").Style.Font.SetItalic();

        Label(ws, 3, 1, "Vereinsname");
        Text(ws, 3, 2, report.ClubName);
        Label(ws, 4, 1, "Geschäftsjahr");
        Label(ws, 4, 2, report.Year.ToString()).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Left);
        Label(ws, 5, 1, "Steuernummer");
        Text(ws, 5, 2, report.TaxNumber);
        Label(ws, 6, 1, "Kassenwart/in");
        Text(ws, 6, 2, report.Treasurer);
        Label(ws, 7, 1, "Zeitraum");
        Label(ws, 7, 2, $"01.01.{report.Year} – 31.12.{report.Year}");

        Label(ws, 9, 1, "Anfangsbestände 01.01.").Style.Font.SetBold();
        Label(ws, 10, 1, EurLabels.Bank);
        Label(ws, 11, 1, EurLabels.Cash);
        foreach (int row in new[] { 10, 11 })
            ws.Cell(row, 2).Style.Fill.SetBackgroundColor(InputFill).NumberFormat.SetFormat(MoneyFormat);

        Label(ws, 13, 1, "Kategorien – Zuordnung zu den vier steuerlichen Bereichen").Style.Font.SetBold();
        Header(ws, 14, 1, 3, "Kategorie", "Sphäre (Bereich)", "Art");

        int categoryRow = 15;
        foreach (EurCategoryDefinition category in report.Categories)
        {
            Text(ws, categoryRow, 1, category.Name);
            Label(ws, categoryRow, 2, EurLabels.AreaName(category.Area));
            Label(ws, categoryRow, 3, EurLabels.Kind(category.IsIncome));
            categoryRow++;
        }
    }

    private static void WriteJournalSheet(IXLWorksheet ws, EurReport report)
    {
        SetWidths(ws, 30, 14, 56, 32, 36, 10, 10, 15, 15, 14);

        Label(ws, 1, 1, "Kassenbuch / Buchungsjournal").Style.Font.SetBold().Font.SetFontSize(16).Font.SetFontColor(TitleColor);
        Text(ws, 2, 1, $"Verein: {report.ClubName}   ·   Geschäftsjahr {report.Year}   ·   Zufluss-/Abflussprinzip, Bruttobeträge");

        Label(ws, 3, 7, "Summen:").Style.Font.SetBold();
        Money(ws, 3, 8, report.Journal.Where(x => x.IsIncome).Sum(x => x.Amount)).Style.Font.SetBold();
        Money(ws, 3, 9, report.Journal.Where(x => !x.IsIncome).Sum(x => x.Amount)).Style.Font.SetBold();

        string[] headers = { "Beleg-Nr.", "Zahlungsdatum", "Buchungstext", "Kategorie", "Sphäre", "Art", "Konto", "Einnahme €", "Ausgabe €", "Prüfung" };
        for (int column = 0; column < headers.Length; column++)
            Label(ws, 4, column + 1, headers[column]);
        ws.Range(4, 1, 4, headers.Length).Style.Font.SetBold().Fill.SetBackgroundColor(HeaderFill);
        ws.Range(4, 8, 4, 9).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right);

        int row = 5;
        foreach (EurJournalLine line in report.Journal)
        {
            Label(ws, row, 1, line.Number);
            ws.Cell(row, 2).Value = line.Date;
            ws.Cell(row, 2).Style.DateFormat.SetFormat(DateFormat);
            Text(ws, row, 3, line.Text);
            Text(ws, row, 4, line.Category);
            Label(ws, row, 5, EurLabels.AreaName(line.Area)).Style.Fill.SetBackgroundColor(AreaColor(line.Area));
            Label(ws, row, 6, EurLabels.Kind(line.IsIncome));
            Text(ws, row, 7, line.Account);
            Money(ws, row, line.IsIncome ? 8 : 9, line.Amount);
            Label(ws, row, 10, line.Check).Style.Font.SetFontColor(line.Check == "OK" ? OkFont : FailFont);
            row++;
        }

        ws.Range(4, 1, Math.Max(row - 1, 5), headers.Length).SetAutoFilter();
        ws.SheetView.FreezeRows(4);
    }

    private static void WriteContributionProofSheet(IXLWorksheet ws, EurReport report)
    {
        SetWidths(ws, 18, 16, 16, 16, 15);

        Label(ws, 1, 1, "Beitragsnachweis").Style.Font.SetBold().Font.SetFontSize(16).Font.SetFontColor(TitleColor);
        Text(ws, 2, 1, $"Verein: {report.ClubName}   ·   Geschäftsjahr {report.Year}   ·   Einzelbeiträge zu den Sammelzeilen „Beitragseinzug“ im Journal");

        string[] headers = { "Beleg-Nr.", "Zahlungsdatum", "Fälligkeit", "Mitglieds-Nr.", "Betrag €" };
        for (int column = 0; column < headers.Length; column++)
            Label(ws, 4, column + 1, headers[column]);
        ws.Range(4, 1, 4, headers.Length).Style.Font.SetBold().Fill.SetBackgroundColor(HeaderFill);
        ws.Cell(4, 5).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right);

        int row = 5;
        foreach (EurContributionProof proof in report.ContributionProofs)
        {
            Label(ws, row, 1, proof.Number);
            ws.Cell(row, 2).Value = proof.PaymentDate;
            ws.Cell(row, 2).Style.DateFormat.SetFormat(DateFormat);
            ws.Cell(row, 3).Value = proof.DueDate;
            ws.Cell(row, 3).Style.DateFormat.SetFormat(DateFormat);
            ws.Cell(row, 4).Value = proof.MemberNumber;
            Money(ws, row, 5, proof.Amount);
            row++;
        }

        ws.Range(4, 1, Math.Max(row - 1, 5), headers.Length).SetAutoFilter();
        ws.SheetView.FreezeRows(4);
    }

    private static void WriteNotesSheet(IXLWorksheet ws)
    {
        ws.Column(1).Width = 110;

        Label(ws, 1, 1, "So ist die Vereins-EÜR aufgebaut").Style.Font.SetBold().Font.SetFontSize(16).Font.SetFontColor(TitleColor);

        string[] lines =
        {
            "1. Journal: Jede bezahlte Einnahme und Ausgabe des Geschäftsjahres mit Beleg-Nr., Datum, Text, Kategorie, Konto und Bruttobetrag (Zufluss-/Abflussprinzip, § 11 EStG); maßgeblich ist das Zahlungsdatum, nicht Beleg- oder Fälligkeitsdatum. Mitgliedsbeiträge werden je Zahlungsdatum zu einer Zeile zusammengefasst, deren Einzelbeiträge das Blatt „Beitragsnachweis“ aufführt.",
            "1a. Beleg-Nr.: E-JJJJ-NNNN für Einnahmen und Beiträge, A-JJJJ-NNNN für Ausgaben. Die Nummer wird beim Buchen als bezahlt einmalig vergeben, ändert sich nie und wird nicht neu vergeben.",
            "2. EÜR: Einnahmen und Ausgaben werden getrennt nach den vier Bereichen eines gemeinnützigen Vereins summiert.",
            "3. Stammdaten: Vereinsdaten, Anfangsbestände und Kategorien mit Sphäre und Art. Die Sphäre je Kategorie wird in der Kategorie-Verwaltung der App gepflegt.",
            "",
            "Legende",
            "Gelb hinterlegte Zellen sind leer: Anfangsbestände und der Abgleich Kasse / Bank werden von der App nicht geführt und können von Hand ergänzt werden. Alle Zahlen dieser Datei sind berechnete Werte, keine Formeln.",
            "",
            "Hinweis: Automatisch aus den Daten der Vereinsverwaltung erzeugt – ersetzt keine Steuerberatung. Umsatzsteuer (Kleinunternehmerregelung) ist hier nicht abgebildet."
        };

        string[] headings = { "Legende" };
        for (int index = 0; index < lines.Length; index++)
        {
            IXLCell cell = Label(ws, index + 2, 1, lines[index]);
            cell.Style.Alignment.SetWrapText();
            if (headings.Contains(lines[index]))
                cell.Style.Font.SetBold();
        }
    }

    private static string HeaderLine(EurReport report)
    {
        return string.IsNullOrWhiteSpace(report.TaxNumber)
            ? report.ClubName
            : $"{report.ClubName}   ·   Steuernummer {report.TaxNumber}";
    }

    private static XLColor AreaColor(TaxArea area) => area switch
    {
        TaxArea.NON_PROFIT => XLColor.FromHtml("#E2F0D9"),
        TaxArea.ASSET_MANAGEMENT => XLColor.FromHtml("#DDEBF7"),
        TaxArea.PURPOSE_RELATED_OPERATION => XLColor.FromHtml("#FFF2CC"),
        _ => XLColor.FromHtml("#FCE4D6")
    };

    private static void SetWidths(IXLWorksheet ws, params double[] widths)
    {
        for (int index = 0; index < widths.Length; index++)
            ws.Column(index + 1).Width = widths[index];
    }

    private static void Header(IXLWorksheet ws, int row, int firstColumn, int count, params string[] titles)
    {
        for (int index = 0; index < count; index++)
            Label(ws, row, firstColumn + index, titles[index]);

        IXLRange range = ws.Range(row, firstColumn, row, firstColumn + count - 1);
        range.Style.Font.SetBold().Fill.SetBackgroundColor(HeaderFill);
        if (count > 1)
            ws.Range(row, firstColumn + 1, row, firstColumn + count - 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right);
    }

    private static IXLCell Label(IXLWorksheet ws, int row, int column, string value)
    {
        IXLCell cell = ws.Cell(row, column);
        cell.Value = value;
        return cell;
    }

    private static IXLCell Text(IXLWorksheet ws, int row, int column, string? value)
    {
        IXLCell cell = ws.Cell(row, column);
        cell.Value = CsvSanitizer.Sanitize(value) ?? string.Empty;
        return cell;
    }

    private static IXLCell Money(IXLWorksheet ws, int row, int column, decimal value)
    {
        IXLCell cell = ws.Cell(row, column);
        cell.Value = value;
        cell.Style.NumberFormat.SetFormat(MoneyFormat);
        return cell;
    }

    private static IXLCell Percent(IXLWorksheet ws, int row, int column, decimal value)
    {
        IXLCell cell = ws.Cell(row, column);
        cell.Value = value;
        cell.Style.NumberFormat.SetFormat(PercentFormat);
        return cell;
    }
}
