using System;

namespace UniVerein.Api.Security;

public static class LoginGuard
{
    private const int ONE_DAY_IN_SECONDS = 86400;

    public static DateTime? GetLockoutReleaseTime(int failedAttempts)
    {
        if (failedAttempts < 3)
        {
            return null;
        }

        double secondsToWait = Math.Min(30 * Math.Pow(2, failedAttempts - 3), ONE_DAY_IN_SECONDS);
        return DateTime.UtcNow.AddSeconds(secondsToWait);
    }
}