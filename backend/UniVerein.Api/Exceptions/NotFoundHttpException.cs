using System;
using System.Net;

namespace UniVerein.Api.Exceptions;

public class NotFoundHttpException : BaseHttpException
{
    public NotFoundHttpException(string errorCode, Exception? outerException = null,
        string errorMessage = ApiErrorCodes.RESOURCE_NOT_FOUND, string? moreInfo = null)
        : base(errorCode, HttpStatusCode.NotFound, outerException, errorMessage, moreInfo)
    {
    }
}