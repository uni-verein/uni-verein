using UniVerein.Api.Security;
using UniVerein.IntegrationTests.Infrastructure;
using Shouldly;
using Xunit;

namespace UniVerein.IntegrationTests.Tests;

public class LoginAttemptTrackerTests
{
    private readonly MutableFakeTimeProvider _time = new();
    private readonly LoginAttemptTracker _tracker;

    public LoginAttemptTrackerTests()
    {
        _tracker = new LoginAttemptTracker(_time);
    }

    [Fact]
    public void GetRemainingLockout_BelowThreeFailures_ReturnsNull()
    {
        // Arrange
        _tracker.RegisterFailure("admin", "203.0.113.1");
        _tracker.RegisterFailure("admin", "203.0.113.1");

        // Act
        TimeSpan? remaining = _tracker.GetRemainingLockout("admin", "203.0.113.1");

        // Assert
        remaining.ShouldBeNull();
    }

    [Fact]
    public void GetRemainingLockout_ThirdFailure_LocksForThirtySeconds()
    {
        // Arrange
        for (int i = 0; i < 3; i++)
            _tracker.RegisterFailure("admin", "203.0.113.1");

        // Act
        TimeSpan? remainingRightAfter = _tracker.GetRemainingLockout("admin", "203.0.113.1");
        _time.SetUtcNow(_time.GetUtcNow().AddSeconds(31));
        TimeSpan? remainingAfterWaiting = _tracker.GetRemainingLockout("admin", "203.0.113.1");

        // Assert
        remainingRightAfter.ShouldBe(TimeSpan.FromSeconds(30));
        remainingAfterWaiting.ShouldBeNull();
    }

    [Fact]
    public void GetRemainingLockout_OtherClientOrOtherUser_IsNotAffected()
    {
        // Arrange
        for (int i = 0; i < 5; i++)
            _tracker.RegisterFailure("admin", "203.0.113.1");

        // Act
        TimeSpan? otherClient = _tracker.GetRemainingLockout("admin", "203.0.113.2");
        TimeSpan? otherUser = _tracker.GetRemainingLockout("other", "203.0.113.1");

        // Assert
        otherClient.ShouldBeNull();
        otherUser.ShouldBeNull();
    }

    [Fact]
    public void GetRemainingLockout_AddressesOfOneIpv6Prefix_ShareTheLockout()
    {
        // Arrange
        for (int i = 0; i < 3; i++)
            _tracker.RegisterFailure("admin", $"2001:db8:1:2::{i + 1}");

        // Act
        TimeSpan? samePrefix = _tracker.GetRemainingLockout("admin", "2001:db8:1:2:aaaa:bbbb:cccc:dddd");
        TimeSpan? otherPrefix = _tracker.GetRemainingLockout("admin", "2001:db8:1:3::1");

        // Assert
        samePrefix.ShouldNotBeNull();
        otherPrefix.ShouldBeNull();
    }

    [Fact]
    public void GetRemainingLockout_Ipv4MappedAddress_SharesTheLockoutWithIpv4()
    {
        // Arrange
        for (int i = 0; i < 3; i++)
            _tracker.RegisterFailure("admin", "203.0.113.1");

        // Act
        TimeSpan? remaining = _tracker.GetRemainingLockout("admin", "::ffff:203.0.113.1");

        // Assert
        remaining.ShouldNotBeNull();
    }

    [Fact]
    public void Reset_RemovesFailuresOfThatClient()
    {
        // Arrange
        for (int i = 0; i < 3; i++)
            _tracker.RegisterFailure("admin", "203.0.113.1");

        // Act
        _tracker.Reset("admin", "203.0.113.1");

        // Assert
        _tracker.GetRemainingLockout("admin", "203.0.113.1").ShouldBeNull();
    }
}
