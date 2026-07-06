using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using UniVerein.Api.ApiResults;
using UniVerein.Api.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace UniVerein.Api.Extensions;

public class GlobalExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception,
        CancellationToken cancellationToken)
    {
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
}