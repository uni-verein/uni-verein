using System;
using MailKit.Security;

namespace UniVerein.Api.Helper;

public static class MailTlsHelper
{
    public const int SmtpImplicitTlsPort = 465;
    public const int ImapImplicitTlsPort = 993;

    public static SecureSocketOptions ResolveSocketOptions(bool enableSsl, int port, int implicitTlsPort)
    {
        if (!enableSsl)
            return SecureSocketOptions.None;

        return port == implicitTlsPort ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
    }

    // MailKit throws NotSupportedException when STARTTLS is required but not offered.
    public static bool IsTlsUnavailable(Exception exception)
    {
        return exception is NotSupportedException or SslHandshakeException;
    }
}
