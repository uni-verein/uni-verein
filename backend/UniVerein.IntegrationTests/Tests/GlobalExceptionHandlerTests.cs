using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Shouldly;
using UniVerein.Api.Exceptions;
using UniVerein.Api.Extensions;
using Xunit;

namespace UniVerein.IntegrationTests.Tests;

public class GlobalExceptionHandlerTests
{
    private readonly GlobalExceptionHandler _handler = new();

    [Fact]
    public async Task BadHttpRequestException_PayloadTooLarge_IsAnswered413()
    {
        // Arrange
        DefaultHttpContext context = CreateContext();

        // Act
        bool handled = await _handler.TryHandleAsync(context,
            new BadHttpRequestException("Request body too large.", StatusCodes.Status413PayloadTooLarge),
            CancellationToken.None);

        // Assert
        handled.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe(StatusCodes.Status413PayloadTooLarge);
        JsonElement body = await ReadBodyAsync(context);
        body.GetProperty("statusCode").GetInt32().ShouldBe(StatusCodes.Status413PayloadTooLarge);
        body.GetProperty("errorCode").GetString().ShouldBe(ApiErrorCodes.FAILED_REQUEST_VALIDATION);
        body.GetProperty("errorMessage").GetString().ShouldBe("Request body too large.");
    }

    [Fact]
    public async Task BadHttpRequestException_OtherClientError_KeepsItsStatusCode()
    {
        // Arrange
        DefaultHttpContext context = CreateContext();

        // Act
        await _handler.TryHandleAsync(context,
            new BadHttpRequestException("Malformed", StatusCodes.Status400BadRequest), CancellationToken.None);

        // Assert
        context.Response.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task UnexpectedException_IsStillAnswered500()
    {
        // Arrange
        DefaultHttpContext context = CreateContext();

        // Act
        await _handler.TryHandleAsync(context, new InvalidOperationException("boom"), CancellationToken.None);

        // Assert
        context.Response.StatusCode.ShouldBe(StatusCodes.Status500InternalServerError);
    }

    private static DefaultHttpContext CreateContext()
    {
        DefaultHttpContext context = new();
        context.Request.Method = "POST";
        context.Request.Path = "/self-enrollment";
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<JsonElement> ReadBodyAsync(DefaultHttpContext context)
    {
        context.Response.Body.Position = 0;
        return (await JsonDocument.ParseAsync(context.Response.Body)).RootElement.Clone();
    }
}
