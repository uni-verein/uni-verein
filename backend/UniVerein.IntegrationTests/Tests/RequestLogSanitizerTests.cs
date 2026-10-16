using Microsoft.AspNetCore.Http;
using Serilog.Events;
using Shouldly;
using UniVerein.Api.Services;
using Xunit;

namespace UniVerein.IntegrationTests.Tests;

public class RequestLogSanitizerTests
{
    [Theory]
    [InlineData("/calendar/feed/abc-DEF_123.ics", "/calendar/feed/<redacted>.ics")]
    [InlineData("/Calendar/Feed/abc-DEF_123.ics", "/Calendar/Feed/<redacted>.ics")]
    [InlineData("/calendar/feed/abc-DEF_123.ics/", "/calendar/feed/<redacted>.ics/")]
    public void Redact_HidesFeedToken(string path, string expected)
    {
        // Act
        string result = RequestLogSanitizer.Redact(path);

        // Assert
        result.ShouldBe(expected);
        result.ShouldNotContain("abc-DEF_123");
    }

    [Theory]
    [InlineData("/calendar/feed/token")]
    [InlineData("/calendar/events")]
    [InlineData("/public/calendar/feed.ics")]
    [InlineData("/members/42")]
    public void Redact_KeepsOtherPaths(string path)
    {
        RequestLogSanitizer.Redact(path).ShouldBe(path);
    }

    [Fact]
    public void GetMessageTemplateProperties_RedactsPathAndKeepsOtherValues()
    {
        // Arrange
        DefaultHttpContext context = new();
        context.Request.Method = "GET";

        // Act
        Dictionary<string, LogEventPropertyValue> properties = RequestLogSanitizer
            .GetMessageTemplateProperties(context, "/calendar/feed/secret.ics", 12.5, 200)
            .ToDictionary(p => p.Name, p => p.Value);

        // Assert
        properties["RequestPath"].ToString().ShouldBe("\"/calendar/feed/<redacted>.ics\"");
        properties["RequestMethod"].ToString().ShouldBe("\"GET\"");
        properties["StatusCode"].ToString().ShouldBe("200");
        properties["Elapsed"].ToString().ShouldBe("12.5");
    }
}
