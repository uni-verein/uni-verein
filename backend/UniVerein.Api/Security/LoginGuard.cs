using System;

namespace UniVerein.Api.Security;

public static class LoginGuard
{
    public const int LockoutWindowSeconds = 900; // 15 minutes

    public static int GetEffectiveFailedAttempts(int failedAttempts, DateTimeOffset? lastFailedLoginAttempt, DateTimeOffset now)
    {
        if (lastFailedLoginAttempt.HasValue && (now - lastFailedLoginAttempt.Value).TotalSeconds > LockoutWindowSeconds)
        {
            return 0;
        }

        return failedAttempts;
    }

    public static DateTimeOffset? GetLockoutReleaseTime(int failedAttempts, DateTimeOffset now)
    {
        if (failedAttempts < 3)
        {
            return null;
        }

        double secondsToWait = Math.Min(30 * Math.Pow(2, failedAttempts - 3), LockoutWindowSeconds);
        return now.AddSeconds(secondsToWait);
    }
}