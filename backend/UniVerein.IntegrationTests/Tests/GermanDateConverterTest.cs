using System.Globalization;
using UniVerein.Api.Converter;
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using Xunit;

namespace UniVerein.IntegrationTests.Tests;

public class GermanDateConverterTest
{
    private readonly GermanDateConverter _converter = new GermanDateConverter();

    [Theory]
    [InlineData("01.01.23", true)] // dd.MM.yy
    [InlineData("01.01.2023", true)] // dd.MM.yyyy
    [InlineData("1.1.23", true)] // d.M.yy
    [InlineData("1.1.2023", true)] // d.M.yyyy
    [InlineData("31.12.23", true)] // dd.MM.yy
    [InlineData("31.12.2023", true)] // dd.MM.yyyy
    [InlineData("NULL", false)] // NULL string
    [InlineData("", false)] // Empty string
    [InlineData(" ", false)] // Whitespace
    [InlineData("test", false)] // Whitespace
    public void ConvertFromString_ShouldHandleVariousFormats(string input, bool shouldConvert)
    {
        // Arrange
        var row = new CsvReader(new StringReader(""), new CsvConfiguration(CultureInfo.InvariantCulture));
        var memberMapData = new MemberMapData(null);

        // Act
        if (shouldConvert)
        {
            var result = _converter.ConvertFromString(input, row, memberMapData);
            Assert.NotNull(result);
            Assert.IsType<DateTimeOffset>(result);
        }
        else
        {
            if (input == "NULL" || string.IsNullOrWhiteSpace(input))
            {
                var result = _converter.ConvertFromString(input, row, memberMapData);
                Assert.Null(result);
            }
            else
            {
                // For invalid formats, it should throw an exception
                Assert.Throws<TypeConverterException>(() =>
                    _converter.ConvertFromString(input, row, memberMapData));
            }
        }
    }

    [Fact]
    public void ConvertFromString_ShouldReturnNullForNullInput()
    {
        // Arrange
        var row = new CsvReader(new StringReader(""), new CsvConfiguration(CultureInfo.InvariantCulture));
        var memberMapData = new MemberMapData(null);

        // Act
        var result = _converter.ConvertFromString(null, row, memberMapData);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void ConvertFromString_ShouldParseCorrectDates()
    {
        // Arrange
        var row = new CsvReader(new StringReader(""), new CsvConfiguration(CultureInfo.InvariantCulture));
        var memberMapData = new MemberMapData(null);

        // Act
        var result1 = _converter.ConvertFromString("01.01.23", row, memberMapData) as DateTimeOffset?;
        var result2 = _converter.ConvertFromString("01.01.2023", row, memberMapData) as DateTimeOffset?;
        var result3 = _converter.ConvertFromString("1.1.23", row, memberMapData) as DateTimeOffset?;

        // Assert
        Assert.Equal(new DateTime(2023, 1, 1), result1?.DateTime);
        Assert.Equal(new DateTime(2023, 1, 1), result2?.DateTime);
        Assert.Equal(new DateTime(2023, 1, 1), result3?.DateTime);
    }
}