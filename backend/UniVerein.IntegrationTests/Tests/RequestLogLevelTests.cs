using Microsoft.AspNetCore.Http;
using Serilog.Events;
using Shouldly;
using UniVerein.Api.Services;
using Xunit;

namespace UniVerein.IntegrationTests.Tests;

public class RequestLogLevelTests
{
    private static DefaultHttpContext CreateContext(string path, int statusCode)
    {
        DefaultHttpContext context = new();
        context.Request.Path = path;
        context.Response.StatusCode = statusCode;
        return context;
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/Health")]
    public void GetLevel_MutesSuccessfulHealthcheck(string path)
    {
        RequestLogLevel.GetLevel(CreateContext(path, 200), 1, null).ShouldBe(LogEventLevel.Verbose);
    }

    [Fact]
    public void GetLevel_KeepsFailingHealthcheck()
    {
        RequestLogLevel.GetLevel(CreateContext("/health", 503), 1, null).ShouldBe(LogEventLevel.Error);
    }

    [Theory]
    [InlineData("/members", 200, LogEventLevel.Information)]
    [InlineData("/members", 401, LogEventLevel.Information)]
    [InlineData("/members", 500, LogEventLevel.Error)]
    public void GetLevel_LogsOtherRequestsByStatus(string path, int statusCode, LogEventLevel expected)
    {
        RequestLogLevel.GetLevel(CreateContext(path, statusCode), 1, null).ShouldBe(expected);
    }

    [Fact]
    public void GetLevel_LogsExceptionAsError()
    {
        RequestLogLevel.GetLevel(CreateContext("/members", 200), 1, new System.InvalidOperationException())
            .ShouldBe(LogEventLevel.Error);
    }
}
