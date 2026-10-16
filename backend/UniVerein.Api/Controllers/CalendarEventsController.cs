using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog;
using UniVerein.Api.ApiRequests;
using UniVerein.Api.ApiResults.Calendar;
using UniVerein.Api.Exceptions;
using UniVerein.Api.Models.Enums;
using UniVerein.Api.Query;
using UniVerein.Api.Services;
using UniVerein.Api.Services.Calendar;
using UniVerein.DAL.Data;
using UniVerein.DAL.Entities;
using UniVerein.DAL.Entities.Enums;

namespace UniVerein.Api.Controllers;

[Authorize]
[ApiController]
[Route("calendar/events")]
[EnableCors("AllowFrontend")]
public class CalendarEventsController : ControllerBase
{
    private static readonly TimeSpan MaxQueryRange = TimeSpan.FromDays(400);

    private readonly AppDbContext _db;
    private readonly AuditService _auditService;
    private readonly CalendarNotificationService _notificationService;
    private readonly TimeProvider _timeProvider;
    private readonly Guid _currentUserId;
    private readonly bool _isAdmin;

    public CalendarEventsController(AppDbContext db, AuditService auditService,
        CalendarNotificationService notificationService, TimeProvider timeProvider, IHttpContextAccessor http)
    {
        _db = db;
        _auditService = auditService;
        _notificationService = notificationService;
        _timeProvider = timeProvider;

        string? userIdClaim = http.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        _currentUserId = Guid.TryParse(userIdClaim, out Guid id) ? id : Guid.Empty;

        string? role = http.HttpContext?.User.FindFirst(ClaimTypes.Role)?.Value;
        _isAdmin = role == nameof(UserRole.ADMIN);
    }

    [HttpGet]
    public async Task<ActionResult<CalendarEventOccurrenceResults>> GetAllAsync([FromQuery] CalendarEventQuery query)
    {
        DateTimeOffset from = query.From.ToUniversalTime();
        DateTimeOffset to = query.To.ToUniversalTime();

        if (to <= from || to - from > MaxQueryRange)
        {
            Log.Warning($"CalendarEventsController: GetAllAsync -> Invalid range {from:O} - {to:O}.");
            return BadRequest(new ApiResults.ErrorResults.BadRequestResult(
                moreInfo: $"'to' must be after 'from' and the range must not exceed {MaxQueryRange.TotalDays} days."));
        }

        IQueryable<CalendarEventEntity> events = CalendarService.VisibleTo(_db.CalendarEvents, _currentUserId, _isAdmin)
            .Include(e => e.Category)
            .Include(e => e.Participants)
            .Where(e => e.StartAt < to && e.SeriesEndAt > from);

        if (query.CategoryIds is { Count: > 0 })
            events = events.Where(e => e.CategoryId != null && query.CategoryIds.Contains(e.CategoryId.Value));

        if (query.MineOnly)
            events = events.Where(e => e.OwnerUserId == _currentUserId ||
                                       e.Participants.Any(p => p.UserId == _currentUserId));

        List<CalendarEventEntity> entities = await events.AsSplitQuery().ToListAsync();

        List<CalendarEventOccurrenceResult> results = entities
            .SelectMany(e => CalendarRecurrence.Expand(e, from, to)
                .Select(o => CalendarService.ToOccurrence(e, o, _currentUserId, _isAdmin)))
            .OrderBy(o => o.Start)
            .ToList();

        return Ok(new CalendarEventOccurrenceResults { Items = results, Total = results.Count });
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<CalendarEventResult>> GetAsync([FromRoute] Guid id)
    {
        CalendarEventEntity? calendarEvent = await LoadEventAsync(id);
        if (calendarEvent == null || !CalendarService.CanView(calendarEvent, _currentUserId, _isAdmin))
            return EventNotFound(id);

        return Ok(await ToResultAsync(calendarEvent));
    }

    [HttpPost]
    public async Task<ActionResult<CalendarEventResult>> CreateAsync([FromBody] CalendarEventRequest request)
    {
        Log.Information($"CalendarEventsController: CreateAsync -> User {_currentUserId} creates a calendar event.");

        ActionResult? validation = await ValidateAsync(request);
        if (validation != null)
            return validation;

        DateTimeOffset now = _timeProvider.GetUtcNow();
        CalendarEventEntity calendarEvent = new()
        {
            Title = request.Title.Trim(),
            OwnerUserId = _currentUserId,
            Version = 1,
            UpdatedAt = now,
            LastModifiedByUserId = _currentUserId
        };
        Apply(calendarEvent, request);
        SyncParticipants(calendarEvent, request);

        List<Guid> invitedUserIds = calendarEvent.Participants.Select(p => p.UserId).ToList();
        foreach (Guid userId in invitedUserIds.Append(_currentUserId))
            calendarEvent.Subscriptions.Add(new CalendarEventSubscriptionEntity { UserId = userId });

        await _db.CalendarEvents.AddAsync(calendarEvent);
        await _db.SaveChangesAsync();

        await _auditService.LogAsync(AuditLogActions.CREATE, nameof(CalendarEventEntity), AuditData(calendarEvent));
        Log.Information($"CalendarEventsController: CreateAsync -> Calendar event {calendarEvent.Id} created.");

        UserEntity? actor = await _db.Users.FindAsync(_currentUserId);
        if (actor != null)
            await _notificationService.NotifyInvitedAsync(calendarEvent, actor, invitedUserIds,
                [InvitationAttachment(calendarEvent)]);

        CalendarEventEntity created = (await LoadEventAsync(calendarEvent.Id))!;
        return Created($"/calendar/events/{created.Id}", await ToResultAsync(created));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<CalendarEventResult>> UpdateAsync([FromRoute] Guid id,
        [FromBody] CalendarEventRequest request)
    {
        Log.Information($"CalendarEventsController: UpdateAsync -> User {_currentUserId} updates calendar event {id}.");

        CalendarEventEntity? calendarEvent = await LoadEventAsync(id);
        if (calendarEvent == null || !CalendarService.CanView(calendarEvent, _currentUserId, _isAdmin))
            return EventNotFound(id);

        if (!CalendarService.CanEdit(calendarEvent, _currentUserId, _isAdmin))
            return Forbidden("You are not allowed to edit this calendar event.");

        if (request.Version == null || request.Version != calendarEvent.Version)
            return VersionConflict(id);

        if (request.Visibility == CalendarEventVisibility.PRIVATE &&
            calendarEvent.Visibility != CalendarEventVisibility.PRIVATE &&
            calendarEvent.OwnerUserId != _currentUserId)
            return Forbidden("Only the owner can make a calendar event private.");

        ActionResult? validation = await ValidateAsync(request);
        if (validation != null)
            return validation;

        CalendarEventSnapshot before = CalendarEventSnapshot.Of(calendarEvent);
        HashSet<Guid> participantsBefore = calendarEvent.Participants.Select(p => p.UserId).ToHashSet();

        Apply(calendarEvent, request);
        SyncParticipants(calendarEvent, request);
        calendarEvent.Version++;

        HashSet<Guid> participantsAfter = calendarEvent.Participants.Select(p => p.UserId).ToHashSet();
        List<Guid> invitedUserIds = participantsAfter.Except(participantsBefore).ToList();
        List<Guid> removedUserIds = participantsBefore.Except(participantsAfter).ToList();
        await SyncSubscriptionsAsync(calendarEvent.Id, invitedUserIds, removedUserIds);
        calendarEvent.UpdatedAt = _timeProvider.GetUtcNow();
        calendarEvent.LastModifiedByUserId = _currentUserId;

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            Log.Warning($"CalendarEventsController: UpdateAsync -> Concurrent update of calendar event {id}.");
            return VersionConflict(id);
        }

        await _auditService.LogAsync(AuditLogActions.UPDATE, nameof(CalendarEventEntity), AuditData(calendarEvent));
        Log.Information($"CalendarEventsController: UpdateAsync -> Calendar event {id} updated.");

        UserEntity? actor = await _db.Users.FindAsync(_currentUserId);
        if (actor != null)
        {
            await _notificationService.NotifyInvitedAsync(calendarEvent, actor, invitedUserIds,
                [InvitationAttachment(calendarEvent)]);
            await _notificationService.NotifyChangedAsync(calendarEvent, actor,
                CalendarService.ChangedFields(before, calendarEvent), invitedUserIds);
        }

        _db.ChangeTracker.Clear();
        CalendarEventEntity updated = (await LoadEventAsync(id))!;
        return Ok(await ToResultAsync(updated));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteAsync([FromRoute] Guid id)
    {
        Log.Information($"CalendarEventsController: DeleteAsync -> User {_currentUserId} deletes calendar event {id}.");

        CalendarEventEntity? calendarEvent = await LoadEventAsync(id);
        if (calendarEvent == null || !CalendarService.CanView(calendarEvent, _currentUserId, _isAdmin))
            return EventNotFound(id);

        if (!CalendarService.CanDelete(calendarEvent, _currentUserId, _isAdmin))
            return Forbidden("You are not allowed to delete this calendar event.");

        List<Guid> subscriberIds = await _notificationService.SubscriberIdsAsync(calendarEvent);

        _db.Remove(calendarEvent);
        await _db.SaveChangesAsync();

        await _auditService.LogAsync(AuditLogActions.SOFT_DELETE, nameof(CalendarEventEntity),
            AuditData(calendarEvent));
        Log.Information($"CalendarEventsController: DeleteAsync -> Calendar event {id} deleted.");

        UserEntity? actor = await _db.Users.FindAsync(_currentUserId);
        if (actor != null)
            await _notificationService.NotifyDeletedAsync(calendarEvent, actor, subscriberIds);

        return NoContent();
    }

    // Removes a single occurrence from a recurring event.
    [HttpPost("{id}/exceptions")]
    public async Task<ActionResult<CalendarEventResult>> AddExceptionAsync([FromRoute] Guid id,
        [FromBody] CalendarEventExceptionRequest request)
    {
        CalendarEventEntity? calendarEvent = await LoadEventAsync(id);
        if (calendarEvent == null || !CalendarService.CanView(calendarEvent, _currentUserId, _isAdmin))
            return EventNotFound(id);

        if (!CalendarService.CanEdit(calendarEvent, _currentUserId, _isAdmin))
            return Forbidden("You are not allowed to edit this calendar event.");

        DateTimeOffset occurrenceStart = request.OccurrenceStart.ToUniversalTime();
        if (string.IsNullOrWhiteSpace(calendarEvent.RecurrenceRule) ||
            !CalendarRecurrence.IsOccurrence(calendarEvent, occurrenceStart))
            return BadRequest(new ApiResults.ErrorResults.BadRequestResult(
                moreInfo: "The given start is not an occurrence of this recurring event."));

        CalendarEventSnapshot before = CalendarEventSnapshot.Of(calendarEvent);
        calendarEvent.RecurrenceExceptions = CalendarRecurrence.FormatExceptions(
            CalendarRecurrence.ParseExceptions(calendarEvent.RecurrenceExceptions).Append(occurrenceStart));
        calendarEvent.Version++;
        calendarEvent.UpdatedAt = _timeProvider.GetUtcNow();
        calendarEvent.LastModifiedByUserId = _currentUserId;

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return VersionConflict(id);
        }

        await _auditService.LogAsync(AuditLogActions.UPDATE, nameof(CalendarEventEntity), AuditData(calendarEvent));

        UserEntity? actor = await _db.Users.FindAsync(_currentUserId);
        if (actor != null)
            await _notificationService.NotifyChangedAsync(calendarEvent, actor,
                CalendarService.ChangedFields(before, calendarEvent), []);

        return Ok(await ToResultAsync(calendarEvent));
    }

    [HttpPut("{id}/rsvp")]
    public async Task<ActionResult<CalendarEventResult>> RsvpAsync([FromRoute] Guid id,
        [FromBody] CalendarRsvpRequest request)
    {
        CalendarEventEntity? calendarEvent = await LoadEventAsync(id);
        if (calendarEvent == null || !CalendarService.CanView(calendarEvent, _currentUserId, _isAdmin))
            return EventNotFound(id);

        CalendarEventParticipantEntity? participant =
            calendarEvent.Participants.FirstOrDefault(p => p.UserId == _currentUserId);
        if (participant == null)
            return Forbidden("Only invited users can reply to a calendar event.");

        if (participant.RsvpStatus != request.Status)
        {
            participant.RsvpStatus = request.Status;
            await _db.SaveChangesAsync();

            UserEntity? actor = await _db.Users.FindAsync(_currentUserId);
            if (actor != null && request.Status != CalendarRsvpStatus.PENDING)
                await _notificationService.NotifyRsvpAsync(calendarEvent, actor, request.Status);
        }

        return Ok(await ToResultAsync(calendarEvent));
    }

    [HttpPost("{id}/subscription")]
    public async Task<ActionResult<CalendarEventResult>> SubscribeAsync([FromRoute] Guid id)
    {
        CalendarEventEntity? calendarEvent = await LoadEventAsync(id);
        if (calendarEvent == null || !CalendarService.CanView(calendarEvent, _currentUserId, _isAdmin))
            return EventNotFound(id);

        bool subscribed = await _db.CalendarEventSubscriptions
            .AnyAsync(s => s.EventId == id && s.UserId == _currentUserId);
        if (!subscribed)
        {
            await _db.CalendarEventSubscriptions.AddAsync(new CalendarEventSubscriptionEntity
            {
                EventId = id,
                UserId = _currentUserId
            });
            await _db.SaveChangesAsync();
        }

        return Ok(await ToResultAsync(calendarEvent));
    }

    [HttpDelete("{id}/subscription")]
    public async Task<ActionResult<CalendarEventResult>> UnsubscribeAsync([FromRoute] Guid id)
    {
        CalendarEventEntity? calendarEvent = await LoadEventAsync(id);
        if (calendarEvent == null || !CalendarService.CanView(calendarEvent, _currentUserId, _isAdmin))
            return EventNotFound(id);

        List<CalendarEventSubscriptionEntity> subscriptions = await _db.CalendarEventSubscriptions
            .Where(s => s.EventId == id && s.UserId == _currentUserId)
            .ToListAsync();
        if (subscriptions.Count > 0)
        {
            _db.RemoveRange(subscriptions);
            await _db.SaveChangesAsync();
        }

        return Ok(await ToResultAsync(calendarEvent));
    }

    private async Task SyncSubscriptionsAsync(Guid eventId, IReadOnlyCollection<Guid> invitedUserIds,
        IReadOnlyCollection<Guid> removedUserIds)
    {
        List<CalendarEventSubscriptionEntity> existing = await _db.CalendarEventSubscriptions
            .Where(s => s.EventId == eventId &&
                        (invitedUserIds.Contains(s.UserId) || removedUserIds.Contains(s.UserId)))
            .ToListAsync();

        foreach (Guid userId in invitedUserIds.Where(u => existing.All(s => s.UserId != u)))
            _db.CalendarEventSubscriptions.Add(new CalendarEventSubscriptionEntity { EventId = eventId, UserId = userId });

        _db.RemoveRange(existing.Where(s => removedUserIds.Contains(s.UserId)));
    }

    private async Task<CalendarEventEntity?> LoadEventAsync(Guid id)
    {
        return await _db.CalendarEvents
            .Include(e => e.Category)
            .Include(e => e.Participants)
            .AsSplitQuery()
            .FirstOrDefaultAsync(e => e.Id == id);
    }

    private async Task<CalendarEventResult> ToResultAsync(CalendarEventEntity calendarEvent)
    {
        HashSet<Guid> userIds = calendarEvent.Participants.Select(p => p.UserId).ToHashSet();
        userIds.Add(calendarEvent.OwnerUserId);
        if (calendarEvent.LastModifiedByUserId is Guid modifiedBy)
            userIds.Add(modifiedBy);

        Dictionary<Guid, string> usernames = await _db.Users
            .IgnoreQueryFilters()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Username);

        bool isSubscribed = await _db.CalendarEventSubscriptions
            .AnyAsync(s => s.EventId == calendarEvent.Id && s.UserId == _currentUserId);

        return CalendarService.ToResult(calendarEvent, _currentUserId, _isAdmin, usernames, isSubscribed);
    }

    private async Task<ActionResult?> ValidateAsync(CalendarEventRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return BadRequest(new ApiResults.ErrorResults.BadRequestResult(moreInfo: "Title is required."));

        if (request.End < request.Start || (request.AllDay && request.End == request.Start))
            return BadRequest(new ApiResults.ErrorResults.BadRequestResult(
                moreInfo: "The end of a calendar event must be after its start."));

        if (!CalendarService.IsValidLocationUrl(request.LocationUrl))
            return BadRequest(new ApiResults.ErrorResults.BadRequestResult(
                moreInfo: "The location URL must be an absolute http(s) URL."));

        if (request.IsPublic && request.Visibility != CalendarEventVisibility.CLUB)
            return BadRequest(new ApiResults.ErrorResults.BadRequestResult(
                moreInfo: "Only club wide calendar events can be public."));

        if (!CalendarRecurrence.IsValidRule(NormalizeRule(request.RecurrenceRule), out string ruleError))
            return BadRequest(new ApiResults.ErrorResults.BadRequestResult(moreInfo: ruleError));

        if (!string.IsNullOrWhiteSpace(request.RecurrenceRule) &&
            request.Start < CalendarRecurrence.EarliestSeriesStart)
            return BadRequest(new ApiResults.ErrorResults.BadRequestResult(
                moreInfo: $"A recurring calendar event must not start before {CalendarRecurrence.EarliestSeriesStart:yyyy-MM-dd}."));

        if (!CalendarRecurrence.IsEvaluable(new CalendarEventEntity
        {
            Title = request.Title,
            StartAt = request.Start.ToUniversalTime(),
            EndAt = request.End.ToUniversalTime(),
            AllDay = request.AllDay,
            RecurrenceRule = NormalizeRule(request.RecurrenceRule)
        }))
            return BadRequest(new ApiResults.ErrorResults.BadRequestResult(
                moreInfo: "The recurrence rule never produces a valid date."));

        if (request.Visibility == CalendarEventVisibility.PRIVATE && request.Participants.Count > 0)
            return BadRequest(new ApiResults.ErrorResults.BadRequestResult(
                moreInfo: "Private calendar events cannot have participants."));

        if (request.CategoryId is Guid categoryId && !await _db.CalendarCategories.AnyAsync(c => c.Id == categoryId))
            return BadRequest(new ApiResults.ErrorResults.BadRequestResult(
                moreInfo: $"Calendar category {categoryId} does not exist."));

        List<Guid> participantIds = request.Participants.Select(p => p.UserId).Distinct().ToList();
        if (participantIds.Count != request.Participants.Count)
            return BadRequest(new ApiResults.ErrorResults.BadRequestResult(
                moreInfo: "Each user can only be added once as participant."));

        int existingUsers = await _db.Users.CountAsync(u => participantIds.Contains(u.Id));
        if (existingUsers != participantIds.Count)
            return BadRequest(new ApiResults.ErrorResults.BadRequestResult(
                moreInfo: "At least one participant does not exist."));

        return null;
    }

    private static string? NormalizeRule(string? rule)
    {
        if (string.IsNullOrWhiteSpace(rule))
            return null;

        string normalized = rule.Trim();
        return normalized.StartsWith("RRULE:", StringComparison.OrdinalIgnoreCase) ? normalized[6..] : normalized;
    }

    private static void Apply(CalendarEventEntity calendarEvent, CalendarEventRequest request)
    {
        string? rule = NormalizeRule(request.RecurrenceRule);
        DateTimeOffset start = request.Start.ToUniversalTime();

        if (rule == null || rule != calendarEvent.RecurrenceRule || start != calendarEvent.StartAt ||
            request.AllDay != calendarEvent.AllDay)
            calendarEvent.RecurrenceExceptions = string.Empty;

        calendarEvent.RecurrenceRule = rule;
        calendarEvent.ReminderMinutesBefore = request.ReminderMinutesBefore;

        calendarEvent.Title = request.Title.Trim();
        calendarEvent.Description = request.Description?.Trim() ?? string.Empty;
        calendarEvent.StartAt = request.Start.ToUniversalTime();
        calendarEvent.EndAt = request.End.ToUniversalTime();
        calendarEvent.AllDay = request.AllDay;
        calendarEvent.LocationName = request.LocationName?.Trim() ?? string.Empty;
        calendarEvent.LocationAddress = request.LocationAddress?.Trim() ?? string.Empty;
        calendarEvent.LocationUrl = request.LocationUrl?.Trim() ?? string.Empty;
        calendarEvent.Visibility = request.Visibility;
        calendarEvent.IsPublic = request.IsPublic;
        calendarEvent.CategoryId = request.CategoryId;
        CalendarRecurrence.AlignStartToRule(calendarEvent);
        calendarEvent.SeriesEndAt = CalendarRecurrence.ComputeSeriesEnd(calendarEvent);
    }

    private void SyncParticipants(CalendarEventEntity calendarEvent, CalendarEventRequest request)
    {
        Dictionary<Guid, CalendarParticipantRole> requested = request.Participants
            .Where(p => p.UserId != calendarEvent.OwnerUserId)
            .ToDictionary(p => p.UserId, p => p.Role);

        foreach (CalendarEventParticipantEntity participant in calendarEvent.Participants.ToList())
        {
            if (requested.TryGetValue(participant.UserId, out CalendarParticipantRole role))
            {
                participant.Role = role;
                requested.Remove(participant.UserId);
            }
            else
            {
                calendarEvent.Participants.Remove(participant);
                _db.Remove(participant);
            }
        }

        foreach ((Guid userId, CalendarParticipantRole role) in requested)
        {
            calendarEvent.Participants.Add(new CalendarEventParticipantEntity { UserId = userId, Role = role });
        }
    }

    private EmailAttachmentRequest InvitationAttachment(CalendarEventEntity calendarEvent)
    {
        string ics = CalendarIcsBuilder.Build(calendarEvent.Title, [calendarEvent], _timeProvider.GetUtcNow());
        return new EmailAttachmentRequest
        {
            FileName = "termin.ics",
            ContentType = "text/calendar",
            Base64Content = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(ics))
        };
    }

    private static object AuditData(CalendarEventEntity calendarEvent)
    {
        return calendarEvent.Visibility == CalendarEventVisibility.PRIVATE
            ? new { CalendarEventId = calendarEvent.Id, calendarEvent.Visibility }
            : new { CalendarEventId = calendarEvent.Id, calendarEvent.Title, calendarEvent.Visibility };
    }

    private NotFoundObjectResult EventNotFound(Guid id)
    {
        return NotFound(new ApiResults.ErrorResults.NotFoundResult(errorCode: ApiErrorCodes.RESOURCE_NOT_FOUND,
            errorMessage: "Calendar event not found.",
            moreInfo: $"Calendar event with ID {id} not found."));
    }

    private ObjectResult Forbidden(string moreInfo)
    {
        return StatusCode(StatusCodes.Status403Forbidden,
            new ApiResults.ErrorResults.ForbiddenRequestResult(moreInfo: moreInfo));
    }

    private ConflictObjectResult VersionConflict(Guid id)
    {
        return Conflict(new ApiResults.ErrorResults.ConflictResult(
            errorCode: ApiErrorCodes.CONFLICT_RESOURCE_MODIFIED,
            errorMessage: "Calendar event was modified in the meantime.",
            moreInfo: $"Calendar event with ID {id} was changed by someone else. Reload it and try again."));
    }
}
