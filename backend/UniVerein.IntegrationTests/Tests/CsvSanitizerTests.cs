using Shouldly;
using UniVerein.Api.Helper;
using Xunit;

namespace UniVerein.IntegrationTests.Tests;

public class CsvSanitizerTests
{
    [Theory]
    [InlineData("=1+1", "'=1+1")]
    [InlineData("+1+1", "'+1+1")]
    [InlineData("-1+1", "'-1+1")]
    [InlineData("@SUM(1+1)", "'@SUM(1+1)")]
    [InlineData("\t=1+1", "'\t=1+1")]
    [InlineData("\r=1+1", "'\r=1+1")]
    [InlineData("Max Mustermann", "Max Mustermann")]
    [InlineData("a=b", "a=b")]
    [InlineData(" =1+1", " =1+1")]
    [InlineData("", "")]
    public void Sanitize_PrefixesOnlyLeadingFormulaCharacters(string input, string expected)
    {
        CsvSanitizer.Sanitize(input).ShouldBe(expected);
    }

    [Fact]
    public void Sanitize_ReturnsNull_ForNull()
    {
        CsvSanitizer.Sanitize(null).ShouldBeNull();
    }

    [Theory]
    [InlineData("+49 172 12345678")]
    [InlineData("+49 (0)172 1234-5678")]
    [InlineData("+49/172/12345678")]
    [InlineData("0172 12345678")]
    public void SanitizePhoneNumber_KeepsPlainPhoneNumbers(string input)
    {
        CsvSanitizer.SanitizePhoneNumber(input).ShouldBe(input);
    }

    [Theory]
    [InlineData("+1+1", "'+1+1")]
    [InlineData("+cmd|' /C calc'!A0", "'+cmd|' /C calc'!A0")]
    [InlineData("=1+1", "'=1+1")]
    [InlineData("-49 172", "'-49 172")]
    [InlineData("@SUM(1)", "'@SUM(1)")]
    public void SanitizePhoneNumber_NeutralisesFormulas(string input, string expected)
    {
        CsvSanitizer.SanitizePhoneNumber(input).ShouldBe(expected);
    }
}
