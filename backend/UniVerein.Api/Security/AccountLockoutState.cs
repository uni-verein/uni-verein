using System;

namespace UniVerein.Api.Security;

internal sealed class AccountLockoutState
{
    public int Failures { get; }
    public DateTimeOffset WindowStart { get; }
    public int Level { get; }
    public DateTimeOffset LastFailure { get; }
    public DateTimeOffset? BlockedUntil { get; }

    public AccountLockoutState(int failures, DateTimeOffset windowStart, int level, DateTimeOffset lastFailure, DateTimeOffset? blockedUntil)
    {
        Failures = failures;
        WindowStart = windowStart;
        Level = level;
        LastFailure = lastFailure;
        BlockedUntil = blockedUntil;
    }
}
