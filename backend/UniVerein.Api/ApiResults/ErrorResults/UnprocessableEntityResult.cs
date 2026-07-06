using System;
using System.Net;

namespace UniVerein.Api.ApiResults.ErrorResults;

public class UnprocessableEntityResult : ErrorDetailsResult
{
    public UnprocessableEntityResult(string errorCode, string errorMessage = "Unprocessable entity.", string? moreInfo = null)
    {
        ErrorMessage = errorMessage;
        StatusCode = (int)HttpStatusCode.UnprocessableEntity;
        ErrorCode = errorCode;
        MoreInfo = moreInfo;
    }
}