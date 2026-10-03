using UniVerein.Api.Services.Sepa;
using Shouldly;
using Xunit;

namespace UniVerein.IntegrationTests.Tests;

public class SepaTextTests
{
    [Theory]
    [InlineData("Müller & Söhne", "Mueller + Soehne")]
    [InlineData("Ärztin Weiß", "Aerztin Weiss")]
    [InlineData("José Ñúñez", "Jose Nunez")]
    [InlineData("Øresund_Æble", "Oresund-AEble")]
    [InlineData("Name    mit   Leerzeichen ", "Name mit Leerzeichen")]
    [InlineData("Emoji 😀 Verein", "Emoji Verein")]
    [InlineData("„Zitat“", "'Zitat'")]
    [InlineData("Mu\u0308ller", "Mueller")]
    [InlineData("Broken \uD83D Surrogate", "Broken Surrogate")]
    [InlineData(null, "")]
    public void Text_ConvertsToSepaCharacterSet(string? input, string expected)
    {
        SepaText.Text(input, 140).ShouldBe(expected);
    }

    [Fact]
    public void Text_CutsToMaxLength_WithoutEllipsis()
    {
        string result = SepaText.Text(new string('a', 100), 70);

        result.ShouldBe(new string('a', 70));
    }

    [Theory]
    [InlineData("20240101120000_1", "20240101120000-1")]
    [InlineData("/MANDATE//1/", "MANDATE/1")]
    [InlineData("MANDAT 1", "MANDAT-1")]
    [InlineData("Mandat-ä", "Mandat-ae")]
    public void Id_ConvertsToSepaIdentifier(string input, string expected)
    {
        SepaText.Id(input).ShouldBe(expected);
    }

    [Fact]
    public void Id_CutsTo35Characters()
    {
        SepaText.Id(new string('1', 50)).Length.ShouldBe(35);
    }

    [Theory]
    [InlineData("de", "DE")]
    [InlineData(" AT ", "AT")]
    [InlineData("Germany", null)]
    [InlineData("D1", null)]
    [InlineData(null, null)]
    public void CountryCode_ReturnsUpperCaseIsoCodeOrNull(string? input, string? expected)
    {
        SepaText.CountryCode(input).ShouldBe(expected);
    }

    [Theory]
    [InlineData("cobadeffxxx", "COBADEFFXXX")]
    [InlineData("BYLA DEM1 001", "BYLADEM1001")]
    [InlineData("COBADEFF", "COBADEFF")]
    [InlineData("1234DEFF", "1234DEFF")]
    [InlineData("NOTPROVIDED1", null)]
    [InlineData("", null)]
    public void Bic_ReturnsNormalizedBicOrNull(string? input, string? expected)
    {
        SepaText.Bic(input).ShouldBe(expected);
    }

    [Fact]
    public void Text_OnlyContainsEpcLatinCharacterSet()
    {
        string result = SepaText.Text("Ä Ö Ü ä ö ü ß & * $ % @ # ! ; = _ \" € é ñ ø æ Ł 漢字", 140);

        result.ShouldMatch(@"^[a-zA-Z0-9/\-?:().,'+ ]*$");
    }

    [Theory]
    [InlineData("de98 zzz 0999 9999 999", "DE98ZZZ09999999999")]
    [InlineData("DE98ZZZ09999999999", "DE98ZZZ09999999999")]
    public void CreditorId_RemovesSpacesAndUppercases(string input, string expected)
    {
        SepaText.CreditorId(input).ShouldBe(expected);
    }

    [Theory]
    [InlineData("CH9300762011623852957", null, true)]
    [InlineData("GB29NWBK60161331926819", null, true)]
    [InlineData("DE89370400440532013000", "COBADEFFXXX", false)]
    [InlineData("DE89370400440532013000", null, false)]
    [InlineData("GB29NWBK60161331926819", "ABCDJESHXXX", true)]
    [InlineData("FR7630006000011234567890189", "ABCDPMPMXXX", true)]
    [InlineData("FR7630006000011234567890189", null, false)]
    public void IsNonEeaPsp_DetectsSepaCountriesOutsideEea(string iban, string? bic, bool expected)
    {
        SepaCountries.IsNonEeaPsp(iban, bic).ShouldBe(expected);
    }
}
