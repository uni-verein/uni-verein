using System;
using UniVerein.Api.Security;
using Shouldly;
using Xunit;

namespace UniVerein.IntegrationTests.Tests;

public class LoginGuardTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(0, null)]
    [InlineData(1, null)]
    [InlineData(2, null)]
    [InlineData(3, 30)]
    [InlineData(4, 60)]
    [InlineData(5, 120)]
    [InlineData(6, 240)]
    public void GetLockoutReleaseTime_BelowCap_FollowsExponentialBackoff(int failedAttempts, int? expectedSeconds)
    {
        DateTimeOffset? release = LoginGuard.GetLockoutReleaseTime(failedAttempts, Now);

        if (expectedSeconds == null)
        {
            release.ShouldBeNull();
        }
        else
        {
            release.ShouldBe(Now.AddSeconds(expectedSeconds.Value));
        }
    }

    [Theory]
    [InlineData(10)]
    [InlineData(20)]
    [InlineData(100)]
    public void GetLockoutReleaseTime_LargeFailedAttempts_CapsAtLockoutWindow(int failedAttempts)
    {
        DateTimeOffset? release = LoginGuard.GetLockoutReleaseTime(failedAttempts, Now);

        release.ShouldBe(Now.AddSeconds(LoginGuard.LockoutWindowSeconds));
    }

    [Fact]
    public void GetEffectiveFailedAttempts_NoPriorFailure_ReturnsUnchanged()
    {
        int effective = LoginGuard.GetEffectiveFailedAttempts(5, null, Now);

        effective.ShouldBe(5);
    }

    [Fact]
    public void GetEffectiveFailedAttempts_LastFailureWithinWindow_ReturnsUnchanged()
    {
        DateTimeOffset lastFailure = Now.AddSeconds(-LoginGuard.LockoutWindowSeconds + 1);

        int effective = LoginGuard.GetEffectiveFailedAttempts(5, lastFailure, Now);

        effective.ShouldBe(5);
    }

    [Fact]
    public void GetEffectiveFailedAttempts_LastFailureOlderThanWindow_ResetsToZero()
    {
        DateTimeOffset lastFailure = Now.AddSeconds(-LoginGuard.LockoutWindowSeconds - 1);

        int effective = LoginGuard.GetEffectiveFailedAttempts(5, lastFailure, Now);

        effective.ShouldBe(0);
    }
}
