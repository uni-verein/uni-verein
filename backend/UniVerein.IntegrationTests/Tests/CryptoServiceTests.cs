using UniVerein.Api.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace UniVerein.IntegrationTests.Tests;

public class CryptoServiceTests
{
    private readonly CryptoService _cryptoService;

    private const string TestCryptoKeyBase64 = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";
    private const string TestHmacKeyBase64 = "AAECBAUGBwgJCgsMDQ4PEBESExQVFhcYGRobHB0eHw==";

    public CryptoServiceTests()
    {
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CRYPTO_KEY"] = TestCryptoKeyBase64,
                ["HMAC_KEY"] = TestHmacKeyBase64,
            })
            .Build();

        _cryptoService = new CryptoService(config);
    }

    // Encrypt / Decrypt (string)

    [Fact]
    public void Encrypt_ReturnsBase64String()
    {
        // Act
        string result = _cryptoService.Encrypt("hello");

        // Assert
        Exception? exception = Record.Exception(() => Convert.FromBase64String(result));
        Assert.Null(exception);
    }

    [Fact]
    public void Encrypt_OutputContainsIvPlusData_LengthAtLeast16Bytes()
    {
        // Act
        string result = _cryptoService.Encrypt("hello");

        // Assert
        Assert.True(Convert.FromBase64String(result).Length >= 32);
    }

    [Fact]
    public void Encrypt_SamePlaintext_ProducesDifferentCiphertext_EachCall()
    {
        // Act
        string first = _cryptoService.Encrypt("hello");
        string second = _cryptoService.Encrypt("hello");

        // Assert
        Assert.NotEqual(first, second);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Decrypt_NullOrWhitespace_ReturnsInputUnchanged(string? input)
    {
        // Act
        string? result = _cryptoService.Decrypt(input!);

        // Assert
        Assert.Equal(input, result);
    }

    [Fact]
    public void Decrypt_InvalidBase64_ThrowsFormatException()
    {
        // Act & Assert
        Assert.Throws<FormatException>(() => _cryptoService.Decrypt("not-valid-base64!!!"));
    }

    [Theory]
    [InlineData("Short text")]
    [InlineData("Hello, this is a long text with numbers 1993")]
    [InlineData("Unicode: äöü 🚀")]
    [InlineData("1234567890")]
    public void RoundTrip_VariousStrings_ReturnOriginal(string plaintext)
    {
        // Act
        string decrypted = _cryptoService.Decrypt(_cryptoService.Encrypt(plaintext))!;

        // Assert
        Assert.Equal(plaintext, decrypted);
    }

    // Encrypt / Decrypt (DateTimeOffset)

    [Fact]
    public void Encrypt_DateTimeOffset_RoundTrip_ReturnsSameUtcInstant()
    {
        // Arrange
        DateTimeOffset currentDate = DateTimeOffset.UtcNow;

        // Act
        DateTimeOffset decrypted = (DateTimeOffset)_cryptoService.DecryptDate(_cryptoService.Encrypt(currentDate))!;

        // Assert
        Assert.Equal(currentDate.ToUniversalTime(), decrypted.ToUniversalTime());
    }

    [Fact]
    public void DecryptDate_InvalidString_ThrowsFormatException()
    {
        // Arrange
        string encrypted = _cryptoService.Encrypt("not-a-date");

        // Act & Assert
        Assert.Throws<FormatException>(() => _cryptoService.DecryptDate(encrypted));
    }

    // Hash 

    [Fact]
    public void Hash_ReturnsDeterministicBase64()
    {
        // Arrange
        string test = "test";

        // Act & Assert
        Assert.Equal(_cryptoService.Hash(test), _cryptoService.Hash(test));
    }

    [Fact]
    public void Hash_ReturnsBase64EncodedString()
    {
        // Arrange
        string result = _cryptoService.Hash("test input");

        // Act
        Exception? exception = Record.Exception(() => Convert.FromBase64String(result));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void Hash_OutputIs32Bytes_HMACSHA256()
    {
        // Arrange
        string result = _cryptoService.Hash("test");

        // Act
        byte[] hashBytes = Convert.FromBase64String(result);

        // Assert
        Assert.Equal(32, hashBytes.Length);
    }

    [Fact]
    public void Hash_NormalizesSpaces()
    {
        // Act & Assert
        Assert.Equal(_cryptoService.Hash("hello world"), _cryptoService.Hash("helloworld"));
    }

    [Fact]
    public void Hash_IsCaseInsensitive()
    {
        // Act & Assert
        Assert.Equal(_cryptoService.Hash("HelloWorld"), _cryptoService.Hash("HELLOWORLD"));
    }

    [Fact]
    public void Hash_DifferentInputs_ProduceDifferentHashes()
    {
        // Act & Assert
        Assert.NotEqual(_cryptoService.Hash("input1"), _cryptoService.Hash("input2"));
    }

    // ── Password Hash ──────────────────────────────────────────────────────────────────

    [Fact]
    public void PasswordHash_ReturnsBase64EncodedString()
    {
        // Arrange
        string result = CryptoService.HashPassword("test input");

        // Act
        Exception? exception = Record.Exception(() => Convert.FromBase64String(result));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void PasswordHash_OutputIs48Bytes_SHA256()
    {
        // Arrange
        string result = CryptoService.HashPassword("test");

        // Act
        byte[] hashBytes = Convert.FromBase64String(result);

        // Assert
        Assert.Equal(48, hashBytes.Length);
    }

    [Fact]
    public void PasswordHash_IsCaseSensitive()
    {
        // Act & Assert
        Assert.NotEqual(CryptoService.HashPassword("HelloWorld"), CryptoService.HashPassword("HELLOWORLD"));
    }

    [Fact]
    public void PasswordHash_SameInput_ProduceDifferentHashes()
    {
        // Act & Assert
        Assert.NotEqual(CryptoService.HashPassword("input1"), CryptoService.HashPassword("input1"));
    }

    [Fact]
    public void VerifyPassword_PasswordShouldBeVerifiedSuccessfully()
    {
        // Act & Assert
        Assert.True(CryptoService.VerifyPassword("input1", CryptoService.HashPassword("input1")));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void VerifyPassword_PasswordInvalid_ReturnsFalse(string input)
    {
        // Act & Assert
        Assert.False(CryptoService.VerifyPassword(input, CryptoService.HashPassword("input1")));
    }

    [Fact]
    public void VerifyPassword_HashInvalid_ReturnsFalse()
    {
        // Act & Assert
        Assert.False(CryptoService.VerifyPassword("password", ""));
    }

    [Fact]
    public void VerifyPassword_HashInvalid_ReturnsInException()
    {
        // Act
        Exception? exception = Record.Exception(() => CryptoService.VerifyPassword("password", "sdsd"));

        // Assert
        Assert.NotNull(exception);
    }
}