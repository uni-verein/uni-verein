using System.Collections.Generic;
using System.Net;
using UniVerein.Api.Exceptions;

namespace UniVerein.Api.ApiResults.ErrorResults;

public class BadRequestResult : ErrorDetailsResult
{
    public BadRequestResult(string errorMessage = "Failed request validation", string? moreInfo = null,
        List<ErrorResultTranslation>? errorTranslation = null)
    {
        ErrorMessage = errorMessage;
        StatusCode = (int)HttpStatusCode.BadRequest;
        ErrorCode = ApiErrorCodes.FAILED_REQUEST_VALIDATION;
        MoreInfo = moreInfo;
        ErrorResultTranslation = errorTranslation ?? [];
    }
}