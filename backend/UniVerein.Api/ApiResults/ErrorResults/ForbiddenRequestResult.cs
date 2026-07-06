using System.Net;
using UniVerein.Api.Exceptions;

namespace UniVerein.Api.ApiResults.ErrorResults;

public class ForbiddenRequestResult : ErrorDetailsResult
{
    public ForbiddenRequestResult(string errorMessage = "Resource forbidden", string? moreInfo = null)
    {
        ErrorMessage = errorMessage;
        StatusCode = (int)HttpStatusCode.Forbidden;
        ErrorCode = ApiErrorCodes.RESOURCE_REQUEST_FORBIDDEN;
        MoreInfo = moreInfo;
    }
}