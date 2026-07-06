using System.Net;
using UniVerein.Api.Exceptions;

namespace UniVerein.Api.ApiResults.ErrorResults;

public class NotFoundResult : ErrorDetailsResult
{
    public NotFoundResult(string errorCode, string errorMessage = ApiErrorCodes.RESOURCE_NOT_FOUND, string? moreInfo = null)
    {
        ErrorMessage = errorMessage;
        StatusCode = (int)HttpStatusCode.NotFound;
        ErrorCode = errorCode;
        MoreInfo = moreInfo;
    }
}