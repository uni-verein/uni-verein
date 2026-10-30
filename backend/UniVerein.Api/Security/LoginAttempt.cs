using System;

namespace UniVerein.Api.Security;

internal sealed class LoginAttempt
{
    public int FailedAttempts { get; }
    public DateTimeOffset LastFailedAttempt { get; }
    public DateTimeOffset? BlockedUntil { get; }

    public LoginAttempt(int failedAttempts, DateTimeOffset lastFailedAttempt, DateTimeOffset? blockedUntil)
    {
        FailedAttempts = failedAttempts;
        LastFailedAttempt = lastFailedAttempt;
        BlockedUntil = blockedUntil;
    }
}
