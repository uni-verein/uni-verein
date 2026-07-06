using UniVerein.Api.Data;
using UniVerein.Api.Validators;
using Xunit;

namespace UniVerein.IntegrationTests.Tests;

public class SepaValidatorTests
{
    [Fact]
    public void ValidateCreditorConfig_ValidConfig_DoesNotThrow()
    {
        CreditorConfig config = CreateValidCreditorConfig();

        var exception = Record.Exception(() => SepaValidator.ValidateCreditorConfig(config));

        Assert.Null(exception);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateCreditorConfig_InvalidName_ThrowsArgumentException(string name)
    {
        CreditorConfig config = CreateValidCreditorConfig(name: name);

        var ex = Assert.Throws<ArgumentException>(() => SepaValidator.ValidateCreditorConfig(config));
        Assert.Contains("Name", ex.Message);
    }

    [Fact]
    public void ValidateCreditorConfig_ValidName_DoesNotThrow()
    {
        CreditorConfig config = CreateValidCreditorConfig(name: "Muster AG");

        var exception = Record.Exception(() => SepaValidator.ValidateCreditorConfig(config));

        Assert.Null(exception);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("INVALIDIBAN")]
    [InlineData("DE123")]
    [InlineData("1234567890123456789012")]
    [InlineData("DE893704004405320130009999999999999")]
    public void ValidateCreditorConfig_InvalidIban_ThrowsArgumentException(string iban)
    {
        CreditorConfig config = CreateValidCreditorConfig(iban: iban);

        var ex = Assert.Throws<ArgumentException>(() => SepaValidator.ValidateCreditorConfig(config));
        Assert.Contains("Iban", ex.Message);
    }

    [Theory]
    [InlineData("DE89370400440532013000")]
    [InlineData("DE89 3704 0044 0532 0130 00")]
    [InlineData("GB29NWBK60161331926819")]
    [InlineData("AT611904300234573201")]
    public void ValidateCreditorConfig_ValidIban_DoesNotThrow(string iban)
    {
        CreditorConfig config = CreateValidCreditorConfig(iban: iban);

        var exception = Record.Exception(() => SepaValidator.ValidateCreditorConfig(config));

        Assert.Null(exception);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("TOO")]
    [InlineData("TOOLONGBIC12")]
    [InlineData("1234DEFF")]
    [InlineData("DEUT12FF")]
    public void ValidateCreditorConfig_InvalidBic_ThrowsArgumentException(string bic)
    {
        CreditorConfig config = CreateValidCreditorConfig(bic: bic!);

        var ex = Assert.Throws<ArgumentException>(() => SepaValidator.ValidateCreditorConfig(config));
        Assert.Contains("Bic", ex.Message);
    }

    [Theory]
    [InlineData("COBADEFF")]
    [InlineData("COBADEFFXXX")]
    [InlineData("SSKMDEMMXXX")]
    public void ValidateCreditorConfig_ValidBic_DoesNotThrow(string bic)
    {
        CreditorConfig config = CreateValidCreditorConfig(bic: bic);

        var exception = Record.Exception(() => SepaValidator.ValidateCreditorConfig(config));

        Assert.Null(exception);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateCreditorConfig_InvalidCreditorId_ThrowsArgumentException(string creditorId)
    {
        CreditorConfig config = CreateValidCreditorConfig(creditorId: creditorId);

        var ex = Assert.Throws<ArgumentException>(() => SepaValidator.ValidateCreditorConfig(config));
        Assert.Contains("CreditorId", ex.Message);
    }

    [Fact]
    public void ValidateCreditorConfig_ValidCreditorId_DoesNotThrow()
    {
        CreditorConfig config = CreateValidCreditorConfig(creditorId: "DE98ZZZ09999999999");

        var exception = Record.Exception(() => SepaValidator.ValidateCreditorConfig(config));

        Assert.Null(exception);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateCreditorConfig_InvalidTownName_ThrowsArgumentException(string townName)
    {
        CreditorConfig config = CreateValidCreditorConfig(townName: townName);

        var ex = Assert.Throws<ArgumentException>(() => SepaValidator.ValidateCreditorConfig(config));
        Assert.Contains("TownName", ex.Message);
    }

    [Fact]
    public void ValidateCreditorConfig_ValidTownName_DoesNotThrow()
    {
        CreditorConfig config = CreateValidCreditorConfig(townName: "Kiel");

        var exception = Record.Exception(() => SepaValidator.ValidateCreditorConfig(config));

        Assert.Null(exception);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("D")]
    [InlineData("DEU")]
    public void ValidateCreditorConfig_InvalidCountry_ThrowsArgumentException(string country)
    {
        CreditorConfig config = CreateValidCreditorConfig(country: country);

        var ex = Assert.Throws<ArgumentException>(() => SepaValidator.ValidateCreditorConfig(config));

        Assert.Contains("Country", ex.Message);
    }

    [Theory]
    [InlineData("DE")]
    [InlineData("AT")]
    [InlineData("GB")]
    public void ValidateCreditorConfig_ValidCountry_DoesNotThrow(string country)
    {
        CreditorConfig config = CreateValidCreditorConfig(country: country);

        var exception = Record.Exception(() => SepaValidator.ValidateCreditorConfig(config));

        Assert.Null(exception);
    }

    [Fact]
    public void ValidateCreditorConfig_InvalidIban_ExceptionMessageContainsIbanValue()
    {
        CreditorConfig config = CreateValidCreditorConfig(bic: "INVALID123");

        var ex = Assert.Throws<ArgumentException>(() => SepaValidator.ValidateCreditorConfig(config));

        Assert.Contains("INVALID123", ex.Message);
    }

    [Fact]
    public void ValidateCreditorConfig_InvalidBic_ExceptionMessageContainsBicValue()
    {
        CreditorConfig config = CreateValidCreditorConfig(bic: "BADBIC");

        var ex = Assert.Throws<ArgumentException>(() => SepaValidator.ValidateCreditorConfig(config));

        Assert.Contains("BADBIC", ex.Message);
    }

    private static CreditorConfig CreateValidCreditorConfig(string? name = null, string? iban = null,
        string? bic = null, string? creditorId = null, string? townName = null, string? country = null)
    {
        return new CreditorConfig
        {
            Name = name ?? "Max Mustermann GmbH",
            Iban = iban ?? "DE89370400440532013000",
            Bic = bic ?? "COBADEFFXXX",
            CreditorId = creditorId ?? "DE98ZZZ09999999999",
            TownName = townName ?? "Berlin",
            Country = country ?? "DE"
        };
    }
}