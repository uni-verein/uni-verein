using System;
using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;

namespace UniVerein.Api.Converter;

public class GermanDateConverter : DefaultTypeConverter
{
    private static readonly string[] Formats = ["dd.MM.yy", "dd.MM.yyyy", "d.M.yy", "d.M.yyyy"];
    private const string WriteFormat = "dd.MM.yyyy";

    public override object? ConvertFromString(string? text, IReaderRow row, MemberMapData memberMapData)
    {
        if (string.IsNullOrWhiteSpace(text) || text == "NULL")
            return null;

        if (DateTimeOffset.TryParseExact(text, Formats, CultureInfo.GetCultureInfo("de-DE"), DateTimeStyles.None,
                out var date))
            return date;

        return base.ConvertFromString(text, row, memberMapData);
    }

    public override string? ConvertToString(object? value, IWriterRow row, MemberMapData memberMapData)
    {
        if (value is DateTimeOffset date)
            return date.ToLocalTime().ToString(WriteFormat, CultureInfo.GetCultureInfo("de-DE"));

        return base.ConvertToString(value, row, memberMapData);
    }
}