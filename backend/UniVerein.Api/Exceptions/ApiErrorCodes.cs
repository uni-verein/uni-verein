namespace UniVerein.Api.Exceptions;

public static class ApiErrorCodes
{
    public const string RESOURCE_REQUEST_FORBIDDEN = "RESOURCE_REQUEST_FORBIDDEN";
    public const string FAILED_REQUEST_VALIDATION = "FAILED_REQUEST_VALIDATION";
    public const string RESOURCE_NOT_FOUND = "RESOURCE_NOT_FOUND";
    public const string CONFLICT_RESOURCE_ALREADY_EXISTS = "CONFLICT_RESOURCE_ALREADY_EXISTS";
    public const string CONFLICT_RESOURCE_MODIFIED = "CONFLICT_RESOURCE_MODIFIED";
    public const string UNPROCESSABLE_ENTITY = "UNPROCESSABLE_ENTITY";
    public const string INTERNAL_SERVER_ERROR = "INTERNAL_SERVER_ERROR";
    public const string DEMO_MODE_DISABLED = "DEMO_MODE_DISABLED";
    public const string MAIL_TLS_UNAVAILABLE = "MAIL_TLS_UNAVAILABLE";
    public const string MAIL_CONNECTION_FAILED = "MAIL_CONNECTION_FAILED";
    public const string MAIL_AUTH_FAILED = "MAIL_AUTH_FAILED";
    public const string MAIL_SEND_FAILED = "MAIL_SEND_FAILED";
}