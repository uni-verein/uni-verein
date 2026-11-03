using Shouldly;
using UniVerein.Api.Services.LoginNotification;
using UniVerein.IntegrationTests.Infrastructure;
using Xunit;

namespace UniVerein.IntegrationTests.Tests;

public class LoginNotificationQueueTests
{
    private readonly MutableFakeTimeProvider _time = new();
    private readonly LoginNotificationQueue _queue;

    public LoginNotificationQueueTests()
    {
        _queue = new LoginNotificationQueue(_time);
    }

    private LoginNotificationJob NewJob(Guid userId)
    {
        return new LoginNotificationJob(userId, "203.0.113.7", "Mozilla/5.0", _time.GetUtcNow(), false);
    }

    [Fact]
    public void Enqueue_SecondJobOfSameUserWithinInterval_IsDropped()
    {
        // Arrange
        Guid userId = Guid.NewGuid();

        // Act
        bool first = _queue.Enqueue(NewJob(userId));
        bool second = _queue.Enqueue(NewJob(userId));

        // Assert
        first.ShouldBeTrue();
        second.ShouldBeFalse();
    }

    [Fact]
    public void Enqueue_JobsOfDifferentUsers_AreNotLimitedAgainstEachOther()
    {
        // Act
        bool first = _queue.Enqueue(NewJob(Guid.NewGuid()));
        bool second = _queue.Enqueue(NewJob(Guid.NewGuid()));

        // Assert
        first.ShouldBeTrue();
        second.ShouldBeTrue();
    }

    [Fact]
    public void Enqueue_AfterInterval_AcceptsUserAgain()
    {
        // Arrange
        Guid userId = Guid.NewGuid();
        _queue.Enqueue(NewJob(userId));
        _time.SetUtcNow(_time.GetUtcNow() + LoginNotificationQueue.MailInterval);

        // Act
        bool accepted = _queue.Enqueue(NewJob(userId));

        // Assert
        accepted.ShouldBeTrue();
    }
}
