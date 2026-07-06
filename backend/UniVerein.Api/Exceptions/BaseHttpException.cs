using System;
using System.Collections.Generic;
using System.Net;
using UniVerein.Api.ApiResults;

namespace UniVerein.Api.Exceptions;

public class BaseHttpException : Exception
{
    public string ErrorMessage { get; }
    public string MoreInfo { get; }
    public string OuterErrorMessage { get; }
    public int StatusCode { get; }
    public string ErrorCode { get; set; }
    public List<ErrorResultTranslation> ErrorResultTranslation { get; set; }

    public BaseHttpException(string errorCode, HttpStatusCode statusCode, Exception? outerException = null,
        string? errorMessage = null, string? moreInfo = null, List<ErrorResultTranslation>? translation = null)
        : base(errorMessage, outerException)
    {
        ErrorMessage = errorMessage ?? string.Empty;
        OuterErrorMessage = outerException?.Message ?? string.Empty;
        MoreInfo = moreInfo ?? string.Empty;
        StatusCode = (int)statusCode;
        ErrorCode = string.IsNullOrEmpty(errorCode) ? null! : errorCode;
        ErrorResultTranslation = translation ?? new();
    }
}