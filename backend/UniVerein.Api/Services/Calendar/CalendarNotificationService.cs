using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using UniVerein.Api.ApiRequests;
using UniVerein.DAL.Data;
using UniVerein.DAL.Entities;
using UniVerein.DAL.Entities.Enums;

namespace UniVerein.Api.Services.Calendar;

public class CalendarNotificationService
{
    private const string AllDayDetail = "allDay";

    private readonly AppDbContext _db;
    private readonly CalendarMailQueue _mailQueue;

    public CalendarNotificationService(AppDbContext db, CalendarMailQueue mailQueue)
    {
        _db = db;
        _mailQueue = mailQueue;
    }

    public async Task NotifyInvitedAsync(CalendarEventEntity calendarEvent, UserEntity actor,
        IReadOnlyCollection<Guid> invitedUserIds, List<EmailAttachmentRequest>? attachments = null)
    {
        List<Guid> recipients = invitedUserIds.Where(id => id != actor.Id).Distinct().ToList();
        if (recipients.Count == 0)
            return;

        await AddNotifications(recipients, NotificationType.CALENDAR_INVITATION, calendarEvent, actor.Username,
            string.Empty);

        string? link = await EventLinkAsync(calendarEvent.Id);
        _mailQueue.Enqueue(new CalendarMailJob(recipients, UserSettingType.CALENDAR_INVITE_EMAIL,
            CalendarMailBuilder.BuildInvitation(calendarEvent, actor.Username, link, attachments), actor.Id));
    }

    public async Task NotifyChangedAsync(CalendarEventEntity calendarEvent, UserEntity actor,
        IReadOnlyCollection<string> changedFields, IReadOnlyCollection<Guid> excludedUserIds)
    {
        if (changedFields.Count == 0)
            return;

        List<Guid> recipients = (await SubscriberIdsAsync(calendarEvent))
            .Where(id => id != actor.Id && !excludedUserIds.Contains(id))
            .ToList();
        if (recipients.Count == 0)
            return;

        await AddNotifications(recipients, NotificationType.CALENDAR_EVENT_CHANGED, calendarEvent, actor.Username,
            string.Join(",", changedFields));

        string? link = await EventLinkAsync(calendarEvent.Id);
        _mailQueue.Enqueue(new CalendarMailJob(recipients, UserSettingType.CALENDAR_CHANGE_EMAIL,
            CalendarMailBuilder.BuildChange(calendarEvent, actor.Username, changedFields, link), actor.Id));
    }

    public async Task NotifyDeletedAsync(CalendarEventEntity calendarEvent, UserEntity actor,
        IReadOnlyCollection<Guid> subscriberIds)
    {
        List<Guid> recipients = subscriberIds.Where(id => id != actor.Id).Distinct().ToList();
        if (recipients.Count == 0)
            return;

        await AddNotifications(recipients, NotificationType.CALENDAR_EVENT_DELETED, calendarEvent, actor.Username,
            string.Empty);

        _mailQueue.Enqueue(new CalendarMailJob(recipients, UserSettingType.CALENDAR_CHANGE_EMAIL,
            CalendarMailBuilder.BuildDeletion(calendarEvent, actor.Username), actor.Id));
    }

    public async Task NotifyRemindersAsync(IReadOnlyCollection<CalendarDueReminder> reminders)
    {
        foreach (CalendarDueReminder reminder in reminders)
        {
            await AddNotifications(reminder.Recipients, NotificationType.CALENDAR_REMINDER, reminder.Event, string.Empty,
                reminder.Event.AllDay ? AllDayDetail : string.Empty, reminder.OccurrenceStart);
        }

        string? baseUrl = await BaseUrlAsync();
        foreach (CalendarDueReminder reminder in reminders.Where(r => r.Recipients.Count > 0))
        {
            _mailQueue.Enqueue(new CalendarMailJob(reminder.Recipients, UserSettingType.CALENDAR_REMINDER_EMAIL,
                CalendarMailBuilder.BuildReminder(reminder.Event, reminder.OccurrenceStart,
                    EventLink(baseUrl, reminder.Event.Id))));
        }
    }

    public async Task NotifyRsvpAsync(CalendarEventEntity calendarEvent, UserEntity actor, CalendarRsvpStatus status)
    {
        if (calendarEvent.OwnerUserId == actor.Id)
            return;

        await AddNotifications([calendarEvent.OwnerUserId], NotificationType.CALENDAR_RSVP, calendarEvent,
            actor.Username, status.ToString());
    }

    public async Task<List<Guid>> SubscriberIdsAsync(CalendarEventEntity calendarEvent)
    {
        List<Guid> subscriberIds = await _db.CalendarEventSubscriptions
            .Where(s => s.EventId == calendarEvent.Id)
            .Select(s => s.UserId)
            .Distinct()
            .ToListAsync();

        Dictionary<Guid, bool> admins = await _db.Users
            .Where(u => subscriberIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Role == UserRole.ADMIN);

        return subscriberIds
            .Where(id => admins.ContainsKey(id) && CalendarService.CanView(calendarEvent, id, admins[id]))
            .ToList();
    }

    private async Task AddNotifications(IEnumerable<Guid> userIds, NotificationType type, CalendarEventEntity calendarEvent,
        string actor, string details, DateTimeOffset? eventStart = null)
    {
        foreach (Guid userId in userIds)
        {
            _db.Notifications.Add(new NotificationEntity
            {
                UserId = userId,
                Type = type,
                EventId = calendarEvent.Id,
                EventTitle = calendarEvent.Title,
                EventStart = eventStart ?? calendarEvent.StartAt,
                ActorUsername = actor,
                Details = details
            });
        }

        await _db.SaveChangesAsync();
    }

    private async Task<string?> EventLinkAsync(Guid eventId)
    {
        return EventLink(await BaseUrlAsync(), eventId);
    }

    private async Task<string?> BaseUrlAsync()
    {
        return await _db.WebPageConfigs
            .Select(c => c.PublicBaseUrl)
            .FirstOrDefaultAsync();
    }

    private static string? EventLink(string? baseUrl, Guid eventId)
    {
        return string.IsNullOrWhiteSpace(baseUrl)
            ? null
            : $"{baseUrl.TrimEnd('/')}/?page=calendar&event={eventId}";
    }
}
