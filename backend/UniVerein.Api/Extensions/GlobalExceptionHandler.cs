using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using UniVerein.Api.ApiResults;
using UniVerein.Api.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Serilog;
using UniVerein.Api.Services;

namespace UniVerein.Api.Extensions;

public class GlobalExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception,
        CancellationToken cancellationToken)
    {
        LogException(httpContext, exception);

        if (exception is BaseHttpException baseException)
        {
            httpContext.Response.StatusCode = baseException.StatusCode;
            await httpContext.Response.WriteAsJsonAsync(new ErrorDetailsResult
            {
                ErrorCode = baseException.ErrorCode,
                StatusCode = baseException.StatusCode,
                ErrorMessage = baseException.ErrorMessage!,
                MoreInfo = baseException.MoreInfo,
                ErrorResultTranslation = baseException.ErrorResultTranslation
            }, cancellationToken);
        }
        else
        {
            httpContext.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            await httpContext.Response.WriteAsJsonAsync(new ErrorDetailsResult
            {
                StatusCode = (int)HttpStatusCode.InternalServerError,
                ErrorMessage = "Internal Server Error.",
                MoreInfo = "Something went wrong. Please try again later or contact our admin."
            }, cancellationToken);
        }

        return true;
    }

    private static void LogException(HttpContext httpContext, Exception exception)
    {
        string method = httpContext.Request.Method;
        string path = RequestLogSanitizer.Redact(httpContext.Request.Path);

        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            Log.Information("GlobalExceptionHandler: {Method} {Path} was cancelled by the client.", method, path);
        }
        else if (exception is BaseHttpException { StatusCode: < 500 } baseException)
        {
            Log.Warning("GlobalExceptionHandler: {Method} {Path} failed with {StatusCode} {ErrorCode}: {ErrorMessage}",
                method, path, baseException.StatusCode, baseException.ErrorCode, baseException.ErrorMessage);
        }
        else
        {
            Log.Error(exception, "GlobalExceptionHandler: Unhandled exception on {Method} {Path}", method, path);
        }
    }
}