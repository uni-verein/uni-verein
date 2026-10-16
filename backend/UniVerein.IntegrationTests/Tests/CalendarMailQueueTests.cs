using Shouldly;
using UniVerein.Api.ApiRequests;
using UniVerein.Api.Services.Calendar;
using UniVerein.DAL.Entities.Enums;
using UniVerein.IntegrationTests.Infrastructure;
using Xunit;

namespace UniVerein.IntegrationTests.Tests;

public class CalendarMailQueueTests
{
    private readonly MutableFakeTimeProvider _time = new();
    private readonly CalendarMailQueue _queue;

    public CalendarMailQueueTests()
    {
        _queue = new CalendarMailQueue(_time);
    }

    private static CalendarMailJob NewJob(Guid? actorId)
    {
        return new CalendarMailJob([Guid.NewGuid()], UserSettingType.CALENDAR_CHANGE_EMAIL, new EmailRequest(),
            actorId);
    }

    [Fact]
    public void Enqueue_DropsJobsOfOneActor_AfterLimit()
    {
        // Arrange
        Guid actorId = Guid.NewGuid();
        int accepted = 0;

        // Act
        for (int i = 0; i < CalendarMailQueue.MaxJobsPerActor + 5; i++)
        {
            if (_queue.Enqueue(NewJob(actorId)))
                accepted++;
        }

        // Assert
        accepted.ShouldBe(CalendarMailQueue.MaxJobsPerActor);
    }

    [Fact]
    public void Enqueue_DoesNotLimitOtherActors()
    {
        // Arrange
        for (int i = 0; i < CalendarMailQueue.MaxJobsPerActor; i++)
            _queue.Enqueue(NewJob(Guid.NewGuid()));
        Guid limitedActor = Guid.NewGuid();
        for (int i = 0; i < CalendarMailQueue.MaxJobsPerActor; i++)
            _queue.Enqueue(NewJob(limitedActor));

        // Act
        bool otherActor = _queue.Enqueue(NewJob(Guid.NewGuid()));
        bool limited = _queue.Enqueue(NewJob(limitedActor));

        // Assert
        otherActor.ShouldBeTrue();
        limited.ShouldBeFalse();
    }

    [Fact]
    public void Enqueue_DoesNotLimitSystemMails()
    {
        // Act
        int accepted = 0;
        for (int i = 0; i < CalendarMailQueue.MaxJobsPerActor + 5; i++)
        {
            if (_queue.Enqueue(NewJob(null)))
                accepted++;
        }

        // Assert
        accepted.ShouldBe(CalendarMailQueue.MaxJobsPerActor + 5);
    }

    [Fact]
    public void Enqueue_AcceptsJobsAgain_AfterLimitWindow()
    {
        // Arrange
        Guid actorId = Guid.NewGuid();
        for (int i = 0; i < CalendarMailQueue.MaxJobsPerActor; i++)
            _queue.Enqueue(NewJob(actorId));
        _queue.Enqueue(NewJob(actorId)).ShouldBeFalse();

        // Act
        _time.SetUtcNow(DateTimeOffset.UtcNow + CalendarMailQueue.MailLimitWindow + TimeSpan.FromMinutes(1));
        bool result = _queue.Enqueue(NewJob(actorId));

        // Assert
        result.ShouldBeTrue();
    }
}
