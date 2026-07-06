using System.Net;

namespace UniVerein.Api.ApiResults.ErrorResults;

public class ConflictResult : ErrorDetailsResult
{
    public ConflictResult(string errorCode, string errorMessage = "Resource already exists", string? moreInfo = null)
    {
        ErrorCode = errorCode;
        StatusCode = (int)HttpStatusCode.Conflict; 
        ErrorMessage = errorMessage; 
        MoreInfo = moreInfo;
    }
}