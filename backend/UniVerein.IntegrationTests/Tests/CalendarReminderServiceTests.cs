using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using UniVerein.Api.Services.Calendar;
using UniVerein.DAL.Data;
using UniVerein.DAL.Entities;
using UniVerein.DAL.Entities.Enums;
using UniVerein.IntegrationTests.Infrastructure;
using Xunit;

namespace UniVerein.IntegrationTests.Tests;

public class CalendarReminderServiceTests : IntegrationTestBase
{
    private static readonly DateTimeOffset Now = new(2026, 10, 14, 15, 0, 0, TimeSpan.Zero);

    private Guid _ownerId;
    private Guid _attendeeId;
    private Guid _declinedId;

    public CalendarReminderServiceTests(UniVereinWebApplicationFactory factory) : base(factory)
    {
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        Factory.FakeTime.SetUtcNow(Now);
        _ownerId = Guid.NewGuid();
        _attendeeId = Guid.NewGuid();
        _declinedId = Guid.NewGuid();

        await WithDbContext(async db =>
        {
            db.Notifications.RemoveRange(db.Notifications.IgnoreQueryFilters());
            db.CalendarReminderLogs.RemoveRange(db.CalendarReminderLogs.IgnoreQueryFilters());
            db.CalendarEventSubscriptions.RemoveRange(db.CalendarEventSubscriptions.IgnoreQueryFilters());
            db.CalendarEventParticipants.RemoveRange(db.CalendarEventParticipants.IgnoreQueryFilters());
            db.CalendarEvents.RemoveRange(db.CalendarEvents.IgnoreQueryFilters());
            await db.ForceSaveChangesAsync();

            foreach ((Guid id, string name) in new[] { (_ownerId, "owner"), (_attendeeId, "attendee"), (_declinedId, "declined") })
                db.Users.Add(new UserEntity { Id = id, Username = name, PasswordHash = "x", Role = UserRole.USER });
            await db.SaveChangesAsync();
        });
    }

    public override async Task DisposeAsync()
    {
        Factory.FakeTime.SetUtcNow(DateTimeOffset.UtcNow);
        await base.DisposeAsync();
    }

    private async Task<Guid> AddEventAsync(DateTimeOffset start, int? reminderMinutes, string? rule = null,
        bool allDay = false)
    {
        Guid id = Guid.NewGuid();
        await WithDbContext(async db =>
        {
            CalendarEventEntity calendarEvent = new()
            {
                Id = id,
                Title = "Training",
                StartAt = start,
                EndAt = allDay ? start.AddDays(1) : start.AddHours(2),
                AllDay = allDay,
                OwnerUserId = _ownerId,
                RecurrenceRule = rule,
                ReminderMinutesBefore = reminderMinutes,
                UpdatedAt = Now,
                Participants =
                [
                    new CalendarEventParticipantEntity { UserId = _attendeeId, RsvpStatus = CalendarRsvpStatus.ACCEPTED },
                    new CalendarEventParticipantEntity { UserId = _declinedId, RsvpStatus = CalendarRsvpStatus.DECLINED }
                ]
            };
            calendarEvent.SeriesEndAt = CalendarRecurrence.ComputeSeriesEnd(calendarEvent);
            db.CalendarEvents.Add(calendarEvent);
            await db.SaveChangesAsync();
        });
        return id;
    }

    private async Task AddUnevaluableEventAsync(DateTimeOffset start, int reminderMinutes)
    {
        await WithDbContext(async db =>
        {
            db.CalendarEvents.Add(new CalendarEventEntity
            {
                Title = "Broken",
                StartAt = start,
                EndAt = start.AddHours(2),
                OwnerUserId = _ownerId,
                RecurrenceRule = "FREQ=YEARLY;BYMONTH=2;BYMONTHDAY=30",
                ReminderMinutesBefore = reminderMinutes,
                SeriesEndAt = CalendarRecurrence.EndlessSeriesEnd,
                UpdatedAt = Now
            });
            await db.SaveChangesAsync();
        });
    }

    private async Task<int> RunAsync()
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<CalendarReminderService>().SendDueRemindersAsync();
    }

    private async Task<List<NotificationEntity>> RemindersAsync()
    {
        List<NotificationEntity> result = [];
        await WithDbContext(async db =>
        {
            result = await db.Notifications.Where(n => n.Type == NotificationType.CALENDAR_REMINDER).ToListAsync();
        });
        return result;
    }


    [Fact]
    public async Task DueReminder_IsSentOnce_ToOwnerAndAttendeesWhoDidNotDecline()
    {
        // Arrange
        Guid eventId = await AddEventAsync(Now.AddMinutes(30), 60);

        // Act
        int firstRun = await RunAsync();
        int secondRun = await RunAsync();
        List<NotificationEntity> reminders = await RemindersAsync();

        // Assert
        firstRun.ShouldBe(1);
        secondRun.ShouldBe(0);
        reminders.Select(n => n.UserId).Order().ShouldBe(new[] { _ownerId, _attendeeId }.Order());
        reminders.ShouldAllBe(n => n.EventId == eventId && n.EventStart == Now.AddMinutes(30));
    }

    [Fact]
    public async Task EventWithUnevaluableRule_DoesNotBlockOtherReminders()
    {
        // Arrange
        await AddUnevaluableEventAsync(Now.AddMinutes(30), 60);
        Guid eventId = await AddEventAsync(Now.AddMinutes(30), 60);

        // Act
        int sent = await RunAsync();
        List<NotificationEntity> reminders = await RemindersAsync();

        // Assert
        sent.ShouldBe(1);
        reminders.ShouldNotBeEmpty();
        reminders.ShouldAllBe(n => n.EventId == eventId);
    }

    [Fact]
    public async Task ReminderNotYetDue_IsNotSent()
    {
        // Arrange
        await AddEventAsync(Now.AddHours(3), 60);

        // Act
        int beforeDue = await RunAsync();
        Factory.FakeTime.SetUtcNow(Now.AddHours(2).AddMinutes(1));
        int afterDue = await RunAsync();

        // Assert
        beforeDue.ShouldBe(0);
        afterDue.ShouldBe(1);
    }

    [Fact]
    public async Task StartedEventsAndEventsWithoutReminder_AreIgnored()
    {
        // Arrange
        await AddEventAsync(Now.AddMinutes(-10), 60);
        await AddEventAsync(Now.AddMinutes(10), null);

        // Act
        int sent = await RunAsync();

        // Assert
        sent.ShouldBe(0);
    }

    [Fact]
    public async Task ReminderAtStart_IsSentWhenEventStarts()
    {
        // Arrange
        Guid eventId = await AddEventAsync(Now.AddMinutes(3), 0);

        // Act
        int beforeStart = await RunAsync();
        Factory.FakeTime.SetUtcNow(Now.AddMinutes(4));
        int atStart = await RunAsync();
        int secondRun = await RunAsync();
        List<NotificationEntity> reminders = await RemindersAsync();

        // Assert
        beforeStart.ShouldBe(0);
        atStart.ShouldBe(1);
        secondRun.ShouldBe(0);
        reminders.ShouldAllBe(n => n.EventId == eventId && n.EventStart == Now.AddMinutes(3));
    }

    [Fact]
    public async Task ReminderAtStart_IsNotSentForLongStartedEvents()
    {
        // Arrange
        await AddEventAsync(Now.AddMinutes(-30), 0);

        // Act
        int sent = await RunAsync();

        // Assert
        sent.ShouldBe(0);
    }

    [Fact]
    public async Task RecurringEvent_RemindsEachOccurrence()
    {
        // Arrange
        Guid eventId = await AddEventAsync(Now.AddDays(-7).AddMinutes(30), 60, "FREQ=DAILY");

        // Act
        int firstOccurrence = await RunAsync();
        Factory.FakeTime.SetUtcNow(Now.AddDays(1));
        int secondOccurrence = await RunAsync();

        // Assert
        firstOccurrence.ShouldBe(1);
        secondOccurrence.ShouldBe(1);
        await WithDbContext(async db =>
        {
            (await db.CalendarReminderLogs.CountAsync(l => l.EventId == eventId)).ShouldBe(2);
        });
    }

    [Fact]
    public async Task AllDayReminderAtStart_IsSentAtLocalMidnight_NotAtMidnightUtc()
    {
        // Arrange: 2026-10-15 is in summer time, so local midnight is 22:00 UTC of the day before.
        DateTimeOffset storedStart = new(2026, 10, 15, 0, 0, 0, TimeSpan.Zero);
        Factory.FakeTime.SetUtcNow(new DateTimeOffset(2026, 10, 14, 21, 0, 0, TimeSpan.Zero));
        Guid eventId = await AddEventAsync(storedStart, 0, allDay: true);

        // Act
        int beforeLocalMidnight = await RunAsync();
        Factory.FakeTime.SetUtcNow(new DateTimeOffset(2026, 10, 14, 22, 1, 0, TimeSpan.Zero));
        int atLocalMidnight = await RunAsync();
        List<NotificationEntity> reminders = await RemindersAsync();

        // Assert
        beforeLocalMidnight.ShouldBe(0);
        atLocalMidnight.ShouldBe(1);
        reminders.ShouldAllBe(n => n.EventId == eventId && n.EventStart == storedStart && n.Details == "allDay");
    }

    [Fact]
    public async Task AllDayReminderInWinterTime_IsSentAtLocalMidnight()
    {
        // Arrange: 2026-12-15 is in winter time, so local midnight is 23:00 UTC of the day before.
        DateTimeOffset storedStart = new(2026, 12, 15, 0, 0, 0, TimeSpan.Zero);
        Factory.FakeTime.SetUtcNow(new DateTimeOffset(2026, 12, 14, 22, 30, 0, TimeSpan.Zero));
        await AddEventAsync(storedStart, 0, allDay: true);

        // Act
        int beforeLocalMidnight = await RunAsync();
        Factory.FakeTime.SetUtcNow(new DateTimeOffset(2026, 12, 14, 23, 1, 0, TimeSpan.Zero));
        int atLocalMidnight = await RunAsync();

        // Assert
        beforeLocalMidnight.ShouldBe(0);
        atLocalMidnight.ShouldBe(1);
    }

    [Fact]
    public async Task Cleanup_RemovesOldNotifications()
    {
        // Arrange
        await WithDbContext(async db =>
        {
            db.Notifications.Add(new NotificationEntity { UserId = _ownerId, Type = NotificationType.CALENDAR_RSVP });
            await db.SaveChangesAsync();
        });
        Factory.FakeTime.SetUtcNow(Now.AddDays(91));

        // Act
        using IServiceScope scope = Factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<CalendarReminderService>().CleanupAsync();

        // Assert
        await WithDbContext(async db =>
        {
            (await db.Notifications.IgnoreQueryFilters().AnyAsync(n => n.UserId == _ownerId)).ShouldBeFalse();
        });
    }
}
