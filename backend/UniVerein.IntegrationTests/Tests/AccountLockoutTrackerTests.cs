using UniVerein.Api.Security;
using UniVerein.IntegrationTests.Infrastructure;
using Shouldly;
using Xunit;

namespace UniVerein.IntegrationTests.Tests;

public class AccountLockoutTrackerTests
{
    private readonly MutableFakeTimeProvider _time = new();
    private readonly AccountLockoutTracker _tracker;
    private readonly Guid _userId = Guid.NewGuid();

    public AccountLockoutTrackerTests()
    {
        _tracker = new AccountLockoutTracker(_time);
    }

    [Fact]
    public void GetRemainingLockout_BelowMaxFailures_ReturnsNull()
    {
        // Arrange
        for (int i = 0; i < AccountLockoutTracker.MaxFailures - 1; i++)
            Fail(_userId);

        // Act
        TimeSpan? remaining = _tracker.GetRemainingLockout(_userId);

        // Assert
        remaining.ShouldBeNull();
    }

    [Fact]
    public void ReserveAttempt_AtMaxFailures_BlocksForFifteenMinutes()
    {
        // Arrange
        TimeSpan? started = null;
        for (int i = 0; i < AccountLockoutTracker.MaxFailures; i++)
            started = Fail(_userId);

        // Act
        TimeSpan? remaining = _tracker.GetRemainingLockout(_userId);

        // Assert
        started.ShouldBe(TimeSpan.FromMinutes(15));
        remaining.ShouldBe(TimeSpan.FromMinutes(15));
    }

    [Fact]
    public void GetRemainingLockout_AfterTheBlock_ReturnsNullAndCountsFromZero()
    {
        // Arrange
        Block(_userId);
        _time.SetUtcNow(_time.GetUtcNow().AddMinutes(16));

        // Act
        for (int i = 0; i < AccountLockoutTracker.MaxFailures - 1; i++)
            Fail(_userId);

        // Assert
        _tracker.GetRemainingLockout(_userId).ShouldBeNull();
    }

    [Fact]
    public void ReserveAttempt_RepeatedBlocks_DoubleUpToOneHour()
    {
        // Arrange + Act
        List<TimeSpan> blocks = new();
        for (int i = 0; i < 5; i++)
        {
            blocks.Add(Block(_userId));
            _time.SetUtcNow(_time.GetUtcNow().AddMinutes(61));
        }

        // Assert
        blocks.ShouldBe(new[]
        {
            TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(60),
            TimeSpan.FromMinutes(60), TimeSpan.FromMinutes(60)
        });
    }

    [Fact]
    public void ReserveAttempt_AfterADayWithoutFailures_StartsAtTheFirstLevelAgain()
    {
        // Arrange
        Block(_userId);
        Block(_userId, afterMinutes: 61);
        _time.SetUtcNow(_time.GetUtcNow().AddHours(25));

        // Act
        TimeSpan block = Block(_userId);

        // Assert
        block.ShouldBe(TimeSpan.FromMinutes(15));
    }

    [Fact]
    public void ReserveAttempt_FailuresOutsideTheWindow_DoNotAccumulate()
    {
        // Arrange
        for (int i = 0; i < AccountLockoutTracker.MaxFailures - 1; i++)
            Fail(_userId);
        _time.SetUtcNow(_time.GetUtcNow().AddMinutes(16));

        // Act
        TimeSpan? started = Fail(_userId);

        // Assert
        started.ShouldBeNull();
        _tracker.GetRemainingLockout(_userId).ShouldBeNull();
    }

    [Fact]
    public void Reset_RemovesFailuresAndTheBlockLevel()
    {
        // Arrange
        Block(_userId);

        // Act
        _tracker.Reset(_userId);

        // Assert
        _tracker.GetRemainingLockout(_userId).ShouldBeNull();
        Block(_userId).ShouldBe(TimeSpan.FromMinutes(15));
    }

    [Fact]
    public void GetRemainingLockout_OtherAccounts_AreNotAffected()
    {
        // Arrange
        Block(_userId);

        // Act
        TimeSpan? other = _tracker.GetRemainingLockout(Guid.NewGuid());

        // Assert
        other.ShouldBeNull();
    }

    [Fact]
    public void ReserveAttempt_WhileBlocked_IsRefusedAndDoesNotExtendTheBlock()
    {
        // Arrange
        Block(_userId);
        _time.SetUtcNow(_time.GetUtcNow().AddMinutes(5));

        // Act
        TimeSpan? remaining = _tracker.ReserveAttempt(_userId, out TimeSpan? started);

        // Assert
        remaining.ShouldBe(TimeSpan.FromMinutes(10));
        started.ShouldBeNull();
        _tracker.GetRemainingLockout(_userId).ShouldBe(TimeSpan.FromMinutes(10));
    }

    [Fact]
    public void ReserveAttempt_BelowMaxFailures_IsAllowed()
    {
        // Act
        TimeSpan? remaining = _tracker.ReserveAttempt(_userId, out _);

        // Assert
        remaining.ShouldBeNull();
    }

    [Fact]
    public async Task ReserveAttempt_CalledConcurrently_AllowsAtMostMaxFailures()
    {
        // Act
        TimeSpan?[] results = await Task.WhenAll(Enumerable.Range(0, 200)
            .Select(i => Task.Run(() => _tracker.ReserveAttempt(_userId, out _))));

        // Assert
        results.Count(r => r == null).ShouldBe(AccountLockoutTracker.MaxFailures);
        _tracker.GetRemainingLockout(_userId).ShouldBe(TimeSpan.FromMinutes(15));
    }

    private TimeSpan? Fail(Guid userId)
    {
        _tracker.ReserveAttempt(userId, out TimeSpan? started);
        return started;
    }

    private TimeSpan Block(Guid userId, int afterMinutes = 0)
    {
        if (afterMinutes > 0)
            _time.SetUtcNow(_time.GetUtcNow().AddMinutes(afterMinutes));

        TimeSpan? started = null;
        for (int i = 0; i < AccountLockoutTracker.MaxFailures; i++)
            started = Fail(userId);

        return started!.Value;
    }
}
