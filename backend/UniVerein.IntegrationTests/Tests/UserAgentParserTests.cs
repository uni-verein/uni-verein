using Shouldly;
using UniVerein.Api.Services.LoginNotification;
using Xunit;

namespace UniVerein.IntegrationTests.Tests;

public class UserAgentParserTests
{
    [Theory]
    [InlineData("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36", "Chrome", "Windows")]
    [InlineData("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36 Edg/130.0.0.0", "Edge", "Windows")]
    [InlineData("Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.0 Safari/605.1.15", "Safari", "macOS")]
    [InlineData("Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.0 Mobile/15E148 Safari/604.1", "Safari", "iOS")]
    [InlineData("Mozilla/5.0 (iPad; CPU OS 18_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) CriOS/130.0.0.0 Mobile/15E148 Safari/604.1", "Chrome", "iOS")]
    [InlineData("Mozilla/5.0 (Linux; Android 14; Pixel 8) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Mobile Safari/537.36", "Chrome", "Android")]
    [InlineData("Mozilla/5.0 (Linux; Android 14; SM-S918B) AppleWebKit/537.36 (KHTML, like Gecko) SamsungBrowser/25.0 Chrome/121.0.0.0 Mobile Safari/537.36", "Samsung Internet", "Android")]
    [InlineData("Mozilla/5.0 (X11; Linux x86_64; rv:132.0) Gecko/20100101 Firefox/132.0", "Firefox", "Linux")]
    [InlineData("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36 OPR/115.0.0.0", "Opera", "Windows")]
    public void Parse_KnownUserAgent_ReturnsBrowserAndOperatingSystem(string userAgent, string browser, string os)
    {
        // Act
        DeviceDescription device = UserAgentParser.Parse(userAgent);

        // Assert
        device.Browser.ShouldBe(browser);
        device.OperatingSystem.ShouldBe(os);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("curl/8.4.0")]
    public void Parse_MissingOrUnknownUserAgent_ReturnsNothing(string? userAgent)
    {
        // Act
        DeviceDescription device = UserAgentParser.Parse(userAgent);

        // Assert
        device.Browser.ShouldBeNull();
        device.OperatingSystem.ShouldBeNull();
    }
}
