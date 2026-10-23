using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Serilog;
using UniVerein.DAL.Data;
using UniVerein.DAL.Entities;
using UniVerein.DAL.Entities.Enums;

namespace UniVerein.Api.Services.Calendar;

public class CalendarReminderService
{
    private static readonly TimeSpan MaxReminderLead = TimeSpan.FromMinutes(10080);
    private static readonly TimeSpan TriggerGrace = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan AllDayOffsetMargin = TimeSpan.FromDays(1);
    private static readonly TimeSpan NotificationRetention = TimeSpan.FromDays(90);
    private static readonly TimeSpan ReminderLogRetention = TimeSpan.FromDays(30);

    private readonly AppDbContext _db;
    private readonly CalendarNotificationService _notificationService;
    private readonly TimeProvider _timeProvider;

    public CalendarReminderService(AppDbContext db, CalendarNotificationService notificationService,
        TimeProvider timeProvider)
    {
        _db = db;
        _notificationService = notificationService;
        _timeProvider = timeProvider;
    }

    public async Task<int> SendDueRemindersAsync()
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
        DateTimeOffset horizon = now + MaxReminderLead + AllDayOffsetMargin;
        DateTimeOffset graceStart = now - TriggerGrace;

        List<CalendarEventEntity> candidates = await _db.CalendarEvents
            .Where(e => e.ReminderMinutesBefore != null && e.StartAt < horizon && e.SeriesEndAt > graceStart)
            .Select(e => new CalendarEventEntity
            {
                Id = e.Id,
                Title = e.Title,
                StartAt = e.StartAt,
                EndAt = e.EndAt,
                AllDay = e.AllDay,
                TimeZoneId = e.TimeZoneId,
                RecurrenceRule = e.RecurrenceRule,
                RecurrenceExceptions = e.RecurrenceExceptions,
                ReminderMinutesBefore = e.ReminderMinutesBefore
            })
            .ToListAsync();

        List<(Guid EventId, DateTimeOffset Start)> due = candidates
            .SelectMany(e =>
            {
                TimeSpan lead = TimeSpan.FromMinutes(e.ReminderMinutesBefore!.Value);
                return CalendarRecurrence.Expand(e, graceStart, now + lead + AllDayOffsetMargin)
                    .Select(o => (Start: o.Start, Begin: e.AllDay ? CalendarTime.AllDayStart(o.Start, e.TimeZoneId) : o.Start))
                    .Where(o => o.Begin - lead <= now && (o.Begin > now || o.Begin - lead > graceStart))
                    .Select(o => (EventId: e.Id, o.Start));
            })
            .ToList();
        if (due.Count == 0)
            return 0;

        List<Guid> dueEventIds = due.Select(d => d.EventId).Distinct().ToList();
        HashSet<(Guid, DateTimeOffset)> alreadySent = (await _db.CalendarReminderLogs
                .Where(l => dueEventIds.Contains(l.EventId))
                .Select(l => new { l.EventId, l.OccurrenceStart })
                .ToListAsync())
            .Select(l => (l.EventId, l.OccurrenceStart))
            .ToHashSet();

        due = due.Where(d => !alreadySent.Contains((d.EventId, d.Start))).ToList();
        if (due.Count == 0)
            return 0;

        List<Guid> pendingEventIds = due.Select(d => d.EventId).Distinct().ToList();
        Dictionary<Guid, CalendarEventEntity> eventsById = await _db.CalendarEvents
            .Include(e => e.Participants)
            .Include(e => e.Subscriptions)
            .Where(e => pendingEventIds.Contains(e.Id))
            .AsSplitQuery()
            .ToDictionaryAsync(e => e.Id);

        Dictionary<Guid, bool> isAdminByUser = await LoadUsersAsync(eventsById.Values);

        List<CalendarDueReminder> reminders = due
            .Where(d => eventsById.ContainsKey(d.EventId))
            .Select(d => new CalendarDueReminder(eventsById[d.EventId], d.Start,
                Recipients(eventsById[d.EventId], isAdminByUser)))
            .ToList();
        if (reminders.Count == 0)
            return 0;

        foreach (CalendarDueReminder reminder in reminders)
        {
            _db.CalendarReminderLogs.Add(new CalendarReminderLogEntity
            {
                EventId = reminder.Event.Id,
                OccurrenceStart = reminder.OccurrenceStart
            });
        }
        await _db.SaveChangesAsync();
        await _notificationService.NotifyRemindersAsync(reminders);

        foreach (CalendarDueReminder reminder in reminders)
            Log.Information($"CalendarReminderService: Sent reminder for event {reminder.Event.Id} at {reminder.OccurrenceStart:O} to {reminder.Recipients.Count} users.");

        return reminders.Count;
    }

    public async Task CleanupAsync()
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
        DateTimeOffset notificationCutoff = now - NotificationRetention;
        DateTimeOffset logCutoff = now - ReminderLogRetention;

        int notificationCount = await _db.Notifications.IgnoreQueryFilters()
            .Where(n => n.CreatedAt < notificationCutoff)
            .ExecuteDeleteAsync();
        int reminderLogCount = await _db.CalendarReminderLogs.IgnoreQueryFilters()
            .Where(l => l.OccurrenceStart < logCutoff)
            .ExecuteDeleteAsync();

        if (notificationCount > 0 || reminderLogCount > 0)
            Log.Information("CalendarReminderService: CleanupAsync -> Removed {NotificationCount} old notifications and {ReminderLogCount} old reminder logs.",
                notificationCount, reminderLogCount);
    }

    private async Task<Dictionary<Guid, bool>> LoadUsersAsync(IEnumerable<CalendarEventEntity> events)
    {
        List<Guid> userIds = events.SelectMany(CandidateIds).Distinct().ToList();

        return await _db.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Role == UserRole.ADMIN);
    }

    private static List<Guid> Recipients(CalendarEventEntity calendarEvent, Dictionary<Guid, bool> isAdminByUser)
    {
        HashSet<Guid> declined = calendarEvent.Participants
            .Where(p => p.RsvpStatus == CalendarRsvpStatus.DECLINED)
            .Select(p => p.UserId)
            .ToHashSet();

        return CandidateIds(calendarEvent)
            .Distinct()
            .Where(id => isAdminByUser.TryGetValue(id, out bool isAdmin)
                && !declined.Contains(id)
                && CalendarService.CanView(calendarEvent, id, isAdmin))
            .ToList();
    }

    private static IEnumerable<Guid> CandidateIds(CalendarEventEntity calendarEvent)
    {
        return calendarEvent.Participants
            .Where(p => p.RsvpStatus != CalendarRsvpStatus.DECLINED)
            .Select(p => p.UserId)
            .Concat(calendarEvent.Subscriptions.Select(s => s.UserId))
            .Append(calendarEvent.OwnerUserId);
    }
}
